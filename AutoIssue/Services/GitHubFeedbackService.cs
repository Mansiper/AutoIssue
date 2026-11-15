using AutoIssue.Enums;
using AutoIssue.Internal;
using AutoIssue.Internal.Models;
using AutoIssue.Models;
using AutoIssue.Options;
using AutoIssue.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace AutoIssue.Services;

internal sealed class GitHubFeedbackService : IGitHubFeedbackService
{
    private readonly IHttpClientFactory factory;
    private readonly GitHubFeedbackOptions opts;
    private readonly IFeedbackAttachmentStore store;
    private readonly IGitHubHttpProvider http;
    private readonly IAttachmentFileValidator fileValidator;
    private readonly ILogger<GitHubFeedbackService> logger;
    private const int MaxPages = 100;

    public GitHubFeedbackService(
        IHttpClientFactory factory,
        IOptions<GitHubFeedbackOptions> options,
        IFeedbackAttachmentStore store,
        IGitHubHttpProvider http,
        IAttachmentFileValidator fileValidator,
        ILogger<GitHubFeedbackService> logger)
    {
        this.factory = factory;
        opts = options.Value;
        this.store = store;
        this.http = http;
        this.fileValidator = fileValidator;
        this.logger = logger;
    }

    public async Task<FeedbackIssue> CreateFeedback(FeedbackRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var issueOpts = opts.Issues;
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("Message is required.", nameof(request));
        if (request.Message.Length > issueOpts.MaxMessageLength)
            throw new ArgumentException(
                $"Message exceeds the maximum length of {issueOpts.MaxMessageLength} characters.",
                nameof(request));

        var rawAttachments = request.Attachments ?? [];
        if (rawAttachments.Count > 0 && opts.Attachments.IsEnabled)
            AttachmentValidator.Validate(rawAttachments, opts.Attachments, fileValidator);

        // Upload sequentially (parallel commits cause 409 conflicts).
        var uploaded = new List<AttachmentInfo>();
        var failed = new List<string>();
        if (opts.Attachments.IsEnabled)
        {
            foreach (var att in rawAttachments)
            {
                try
                {
                    uploaded.Add(await store.Upload(att, ct).ConfigureAwait(false));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to upload attachment '{File}'.", att.FileName);
                    failed.Add(att.FileName);
                }
            }
        }

        var body = BodyFormatter.BuildBody(request.Message, request.Contact, uploaded, failed);
        var title = string.IsNullOrWhiteSpace(request.Title)
            ? BodyFormatter.GenerateTitle(request.Message)
            : request.Title;

        var labels = new List<string> { issueOpts.FeedbackLabel };
        labels.AddRange(issueOpts.AdditionalLabels);

        var client = factory.CreateClient(HttpClientNames.Issues);
        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"repos/{issueOpts.Owner}/{issueOpts.Repository}/issues");
        req.Content = JsonContent.Create(
            new { Title = title, Body = body, Labels = labels },
            options: http.JsonOptions);

        var issue = await http.Send<GitHubIssueResponse>(client, req, ct)
            .ConfigureAwait(false);
        return Map(issue);
    }

    public async Task<IReadOnlyList<FeedbackIssue>> GetIssues(
        IssueStateFilter state = IssueStateFilter.All, bool feedbackOnly = true, CancellationToken ct = default)
    {
        var issueOpts = opts.Issues;
        var stateStr = state switch
        {
            IssueStateFilter.Open => "open",
            IssueStateFilter.Closed => "closed",
            _ => "all",
        };
        var labelPart = feedbackOnly
            ? $"&labels={Uri.EscapeDataString(issueOpts.FeedbackLabel)}"
            : string.Empty;

        var client = factory.CreateClient(HttpClientNames.Issues);
        var result = new List<FeedbackIssue>();

        for (var page = 1; page <= MaxPages; page++)
        {
            var url = $"repos/{issueOpts.Owner}/{issueOpts.Repository}/issues" +
                      $"?state={stateStr}{labelPart}&per_page=100&page={page}" +
                      "&sort=created&direction=desc";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            var items = await http.Send<GitHubIssueResponse[]>(client, req, ct)
                .ConfigureAwait(false);

            foreach (var item in items)
            {
                if (item.PullRequest.HasValue) continue;
                result.Add(Map(item));
            }

            if (items.Length < 100) break;
        }

        return result;
    }

    public async Task<FeedbackIssue?> GetIssueByNumber(int number, CancellationToken ct = default)
    {
        var issueOpts = opts.Issues;
        var client = factory.CreateClient(HttpClientNames.Issues);
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"repos/{issueOpts.Owner}/{issueOpts.Repository}/issues/{number}");

        var item = await http.SendNullable<GitHubIssueResponse>(client, req, ct)
            .ConfigureAwait(false);
        if (item is null || item.PullRequest.HasValue) return null;
        return Map(item);
    }

    public async Task<FeedbackIssue> UpdateIssueStatus(
        int number, IssueState state, IssueCloseReason? closeReason = null, CancellationToken ct = default)
    {
        var issueOpts = opts.Issues;
        var client = factory.CreateClient(HttpClientNames.Issues);
        using var req = new HttpRequestMessage(HttpMethod.Patch,
            $"repos/{issueOpts.Owner}/{issueOpts.Repository}/issues/{number}");

        if (state == IssueState.Open)
        {
            req.Content = JsonContent.Create(
                new { State = "open", StateReason = "reopened" },
                options: http.JsonOptions);
        }
        else
        {
            var reason = closeReason == IssueCloseReason.NotPlanned ? "not_planned" : "completed";
            req.Content = JsonContent.Create(
                new { State = "closed", StateReason = reason },
                options: http.JsonOptions);
        }

        var item = await http.Send<GitHubIssueResponse>(client, req, ct)
            .ConfigureAwait(false);
        return Map(item);
    }

    public async Task<IReadOnlyList<FeedbackIssue>> GetIssuesByContact(
        string contact, IssueStateFilter state = IssueStateFilter.All, CancellationToken ct = default)
    {
        var all = await GetIssues(state, feedbackOnly: true, ct).ConfigureAwait(false);
        return
        [
            .. all
                .Where(i => i.Contact is not null &&
                            string.Equals(i.Contact, contact, StringComparison.OrdinalIgnoreCase))
        ];
    }

    private static FeedbackIssue Map(GitHubIssueResponse r)
    {
        var (contact, attachments) = BodyFormatter.ParseBody(r.Body);
        return new FeedbackIssue(
            r.Number, r.Id, r.Title, r.Body ?? string.Empty,
            r.State, r.StateReason, contact, attachments,
            r.HtmlUrl, ParseLabels(r.Labels), r.CreatedAt, r.ClosedAt);
    }

    private static IReadOnlyList<string> ParseLabels(JsonElement[] labels)
    {
        var result = new List<string>(labels.Length);
        foreach (var label in labels)
        {
            if (label.ValueKind == JsonValueKind.String)
                result.Add(label.GetString() ?? string.Empty);
            else if (label.ValueKind == JsonValueKind.Object &&
                     label.TryGetProperty("name", out var name))
                result.Add(name.GetString() ?? string.Empty);
        }

        return result;
    }
}