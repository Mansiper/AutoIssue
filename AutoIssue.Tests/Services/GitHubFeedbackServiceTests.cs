using AutoIssue.Enums;
using AutoIssue.Exceptions;
using AutoIssue.Internal;
using AutoIssue.Models;
using AutoIssue.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text.Json;
using AutoIssue.Providers;

namespace AutoIssue.Tests.Services;

public sealed class GitHubFeedbackServiceTests
{
    private static GitHubFeedbackOptions Opts() => new()
    {
        Issues = new IssuesOptions
        {
            Owner = "org",
            Repository = "issues-repo",
            Token = "ghp_x",
            FeedbackLabel = "feedback",
            AdditionalLabels = ["from-website"],
            MaxMessageLength = 5000,
        },
        Attachments = new AttachmentsOptions
        {
            Owner = "org",
            Repository = "files-repo",
            Branch = "main",
            Folder = "feedback",
            AllowedFileTypes = [".png", ".pdf", ".txt"],
            MaxFilesPerFeedback = 5,
            MaxFileSizeBytes = 1_000_000,
            MaxTotalSizeBytes = 3_000_000,
        },
    };

    private static GitHubFeedbackOptions OptsNoAttachments() => new()
    {
        Issues = new IssuesOptions
        {
            Owner = "org",
            Repository = "issues-repo",
            Token = "ghp_x",
            FeedbackLabel = "feedback",
        },
        // Attachments.Repository empty → disabled
    };

    private sealed class DualClientFactory : IHttpClientFactory
    {
        private readonly HttpClient issues;
        private readonly HttpClient attachments;

        public DualClientFactory(HttpMessageHandler issues, HttpMessageHandler attachments)
        {
            this.issues = new HttpClient(issues) { BaseAddress = new Uri("https://api.github.com/") };
            this.attachments = new HttpClient(attachments) { BaseAddress = new Uri("https://api.github.com/") };
        }

        public HttpClient CreateClient(string name) => name switch
        {
            HttpClientNames.Issues => issues,
            HttpClientNames.Attachments => attachments,
            _ => throw new InvalidOperationException($"Unknown client '{name}'.")
        };
    }

    private static (GitHubFeedbackService Svc, FakeHttpHandler IssueH, FakeHttpHandler AttH) Build(
        GitHubFeedbackOptions? opts = null)
    {
        var issueH = new FakeHttpHandler();
        var attH = new FakeHttpHandler();
        var o = opts ?? Opts();
        var provider = new GitHubHttpProvider();
        var validator = new DefaultAttachmentFileValidator();
        var store = new GitHubRepoAttachmentStore(
            new SingleClientFactory(attH, HttpClientNames.Attachments),
            EO.Options.Create(o),
            provider,
            NullLogger<GitHubRepoAttachmentStore>.Instance);
        var svc = new GitHubFeedbackService(
            new DualClientFactory(issueH, attH),
            EO.Options.Create(o),
            store,
            provider,
            validator,
            NullLogger<GitHubFeedbackService>.Instance);
        return (svc, issueH, attH);
    }

    private static string IssueJson(int number = 1, string state = "open",
        string? contact = null, string? label = "feedback") =>
        $$"""
          {
            "number": {{number}},
            "id": {{number * 100L}},
            "title": "Feedback: hello",
            "body": "hello\n\n---\n**Contact:** {{contact ?? "_not provided_"}}\n",
            "state": "{{state}}",
            "state_reason": null,
            "html_url": "https://github.com/org/issues-repo/issues/{{number}}",
            "labels": [{"name":"{{label}}"}],
            "created_at": "2025-01-01T00:00:00Z",
            "closed_at": null
          }
          """;

    private static string IssueListJson(params string[] items) =>
        "[" + string.Join(",", items) + "]";

    [Fact]
    public async Task Create_succeeds_and_returns_issue()
    {
        var (svc, issueH, _) = Build();
        issueH.Enqueue(FakeHttpHandler.Created(IssueJson()));

        var result = await svc.CreateFeedback(new FeedbackRequest("Hello feedback"));
        result.Number.Should().Be(1);
        result.State.Should().Be("open");
    }

    [Fact]
    public async Task Create_sends_title_body_labels()
    {
        var (svc, issueH, _) = Build();
        issueH.Enqueue(FakeHttpHandler.Created(IssueJson()));

        await svc.CreateFeedback(new FeedbackRequest("My message", Title: "Custom title"));

        var body = issueH.SentRequests[0].Body;
        var doc = JsonDocument.Parse(body).RootElement;
        doc.GetProperty("title").GetString().Should().Be("Custom title");
        var labels = doc.GetProperty("labels").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        labels.Should().Contain("feedback");
        labels.Should().Contain("from-website");
    }

    [Fact]
    public async Task Create_generates_title_when_omitted()
    {
        var (svc, issueH, _) = Build();
        issueH.Enqueue(FakeHttpHandler.Created(IssueJson()));

        await svc.CreateFeedback(new FeedbackRequest("Short message"));

        var body = issueH.SentRequests[0].Body;
        var doc = JsonDocument.Parse(body).RootElement;
        doc.GetProperty("title").GetString().Should().StartWith("Feedback: ");
    }

    [Fact]
    public async Task Create_empty_message_throws()
    {
        var (svc, _, _) = Build();
        var act = () => svc.CreateFeedback(new FeedbackRequest(""));
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Create_message_too_long_throws()
    {
        var opts = Opts();
        opts.Issues.MaxMessageLength = 10;
        var (svc, _, _) = Build(opts);
        var act = () => svc.CreateFeedback(new FeedbackRequest(new string('x', 11)));
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Create_null_request_throws()
    {
        var (svc, _, _) = Build();
        var act = () => svc.CreateFeedback(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Create_partial_upload_failure_still_creates_issue()
    {
        var (svc, issueH, attH) = Build();

        attH.Enqueue(FakeHttpHandler.Created(UploadJson("https://github.com/org/files/blob/main/x.png")));
        attH.Enqueue(FakeHttpHandler.Json(HttpStatusCode.InternalServerError, "{\"message\":\"server error\"}"));
        attH.Enqueue(FakeHttpHandler.Created(UploadJson("https://github.com/org/files/blob/main/y.pdf")));
        issueH.Enqueue(FakeHttpHandler.Created(IssueJson()));

        var atts = new FeedbackAttachment[]
        {
            new("img.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            new("bad.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            new("doc.pdf", [.. "%PDF-1"u8]),
        };

        var result = await svc.CreateFeedback(new FeedbackRequest("msg", Attachments: atts));

        result.Number.Should().Be(1);
        issueH.SentRequests[0].Body.Should().Contain("Not uploaded");
    }

    [Fact]
    public async Task Create_ignores_attachments_when_attachments_disabled()
    {
        var (svc, issueH, attH) = Build(OptsNoAttachments());
        issueH.Enqueue(FakeHttpHandler.Created(IssueJson()));

        var atts = new FeedbackAttachment[]
        {
            new("img.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        };

        var result = await svc.CreateFeedback(new FeedbackRequest("msg", Attachments: atts));

        result.Number.Should().Be(1);
        attH.SentRequests.Should().BeEmpty(); // no upload attempted
        issueH.SentRequests.Should().HaveCount(1); // issue still created
    }

    [Fact]
    public async Task Create_no_attachments_section_when_disabled()
    {
        var (svc, issueH, _) = Build(OptsNoAttachments());
        issueH.Enqueue(FakeHttpHandler.Created(IssueJson()));

        await svc.CreateFeedback(new FeedbackRequest("msg"));

        issueH.SentRequests[0].Body.Should().NotContain("Attachments:");
    }

    [Fact]
    public async Task GetIssues_returns_all_pages()
    {
        var (svc, issueH, _) = Build();
        issueH.Enqueue(FakeHttpHandler.Ok(
            IssueListJson([.. Enumerable.Range(1, 100).Select(i => IssueJson(i))])));
        issueH.Enqueue(FakeHttpHandler.Ok(
            IssueListJson(IssueJson(101))));

        var issues = await svc.GetIssues();
        issues.Should().HaveCount(101);
    }

    [Fact]
    public async Task GetIssues_excludes_pull_requests()
    {
        var (svc, issueH, _) = Build();
        var prJson = IssueJson().Replace("\"closed_at\": null", "\"closed_at\": null, \"pull_request\": {}");
        issueH.Enqueue(FakeHttpHandler.Ok(IssueListJson(prJson, IssueJson(2))));

        var issues = await svc.GetIssues();
        issues.Should().ContainSingle();
        issues[0].Number.Should().Be(2);
    }

    [Fact]
    public async Task GetIssues_feedbackOnly_adds_label_to_query()
    {
        var (svc, issueH, _) = Build();
        issueH.Enqueue(FakeHttpHandler.Ok(IssueListJson(IssueJson())));

        await svc.GetIssues(feedbackOnly: true);

        var url = issueH.SentRequests[0].Request.RequestUri!.ToString();
        url.Should().Contain("labels=");
        url.Should().Contain("feedback");
    }

    [Fact]
    public async Task GetIssues_not_feedbackOnly_no_label_filter()
    {
        var (svc, issueH, _) = Build();
        issueH.Enqueue(FakeHttpHandler.Ok(IssueListJson(IssueJson())));

        await svc.GetIssues(feedbackOnly: false);

        var url = issueH.SentRequests[0].Request.RequestUri!.ToString();
        url.Should().NotContain("labels=");
    }

    [Fact]
    public async Task GetByNumber_returns_issue()
    {
        var (svc, issueH, _) = Build();
        issueH.Enqueue(FakeHttpHandler.Ok(IssueJson(42)));

        var issue = await svc.GetIssueByNumber(42);
        issue.Should().NotBeNull();
        issue!.Number.Should().Be(42);
    }

    [Fact]
    public async Task GetByNumber_returns_null_on_404()
    {
        var (svc, issueH, _) = Build();
        issueH.Enqueue(FakeHttpHandler.NotFound());

        var issue = await svc.GetIssueByNumber(999);
        issue.Should().BeNull();
    }

    [Fact]
    public async Task GetByNumber_returns_null_on_410()
    {
        var (svc, issueH, _) = Build();
        issueH.Enqueue(FakeHttpHandler.Gone());

        var issue = await svc.GetIssueByNumber(999);
        issue.Should().BeNull();
    }

    [Fact]
    public async Task GetByNumber_returns_null_for_pull_request()
    {
        var (svc, issueH, _) = Build();
        var prJson = IssueJson().Replace("\"closed_at\": null", "\"closed_at\": null, \"pull_request\": {}");
        issueH.Enqueue(FakeHttpHandler.Ok(prJson));

        var issue = await svc.GetIssueByNumber(1);
        issue.Should().BeNull();
    }

    [Fact]
    public async Task Update_close_sends_completed_state_reason()
    {
        var (svc, issueH, _) = Build();
        issueH.Enqueue(FakeHttpHandler.Ok(IssueJson(1, "closed")));

        await svc.UpdateIssueStatus(1, IssueState.Closed, IssueCloseReason.Completed);

        var body = issueH.SentRequests[0].Body;
        var doc = JsonDocument.Parse(body).RootElement;
        doc.GetProperty("state").GetString().Should().Be("closed");
        doc.GetProperty("state_reason").GetString().Should().Be("completed");
    }

    [Fact]
    public async Task Update_close_not_planned_sends_not_planned()
    {
        var (svc, issueH, _) = Build();
        issueH.Enqueue(FakeHttpHandler.Ok(IssueJson(1, "closed")));

        await svc.UpdateIssueStatus(1, IssueState.Closed, IssueCloseReason.NotPlanned);

        var body = issueH.SentRequests[0].Body;
        var doc = JsonDocument.Parse(body).RootElement;
        doc.GetProperty("state_reason").GetString().Should().Be("not_planned");
    }

    [Fact]
    public async Task Update_reopen_sends_reopened()
    {
        var (svc, issueH, _) = Build();
        issueH.Enqueue(FakeHttpHandler.Ok(IssueJson()));

        await svc.UpdateIssueStatus(1, IssueState.Open);

        var body = issueH.SentRequests[0].Body;
        var doc = JsonDocument.Parse(body).RootElement;
        doc.GetProperty("state").GetString().Should().Be("open");
        doc.GetProperty("state_reason").GetString().Should().Be("reopened");
    }

    [Fact]
    public async Task GetByContact_filters_case_insensitively()
    {
        var (svc, issueH, _) = Build();
        issueH.Enqueue(FakeHttpHandler.Ok(IssueListJson(
            IssueJson(contact: "user@example.com"),
            IssueJson(2, contact: "other@example.com"),
            IssueJson(3, contact: "USER@EXAMPLE.COM"))));

        var issues = await svc.GetIssuesByContact("user@example.com");
        issues.Should().HaveCount(2); // issues 1 and 3
    }

    [Fact]
    public async Task GetByContact_does_not_match_anonymous()
    {
        var (svc, issueH, _) = Build();
        issueH.Enqueue(FakeHttpHandler.Ok(IssueListJson(
            IssueJson(),
            IssueJson(2, contact: "user@example.com"))));

        var issues = await svc.GetIssuesByContact("user@example.com");
        issues.Should().ContainSingle();
    }

    [Fact]
    public async Task Non_success_response_throws_GitHubApiException()
    {
        var (svc, issueH, _) = Build();
        issueH.Enqueue(FakeHttpHandler.Json(HttpStatusCode.UnprocessableContent,
            "{\"message\":\"Validation Failed\"}"));

        var act = () => svc.CreateFeedback(new FeedbackRequest("test"));
        var ex = await act.Should().ThrowAsync<GitHubApiException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.UnprocessableContent);
        ex.Which.Message.Should().Contain("Validation Failed");
    }

    private static string UploadJson(string htmlUrl) =>
        $"{{\"content\":{{\"html_url\":\"{htmlUrl}\",\"path\":\"feedback/2025/01/abc.png\"}}}}";
}