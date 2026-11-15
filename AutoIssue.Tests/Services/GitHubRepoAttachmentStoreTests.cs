using AutoIssue.Exceptions;
using AutoIssue.Internal;
using AutoIssue.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using AutoIssue.Models;
using AutoIssue.Providers;

namespace AutoIssue.Tests.Services;

public sealed class GitHubRepoAttachmentStoreTests
{
    private static GitHubFeedbackOptions Opts(string folder = "feedback") => new()
    {
        Attachments = new AttachmentsOptions
        {
            Owner = "org",
            Repository = "files",
            Branch = "main",
            Folder = folder,
        },
    };

    private static (GitHubRepoAttachmentStore Store, FakeHttpHandler Handler) Build(
        GitHubFeedbackOptions? opts = null)
    {
        var handler = new FakeHttpHandler();
        var factory = new SingleClientFactory(handler, HttpClientNames.Attachments);
        var provider = new GitHubHttpProvider();
        var store = new GitHubRepoAttachmentStore(
            factory, EO.Options.Create(opts ?? Opts()),
            provider,
            NullLogger<GitHubRepoAttachmentStore>.Instance);
        return (store, handler);
    }

    private static byte[] PngBytes() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public async Task Upload_path_contains_folder_year_month_guid_ext()
    {
        var (store, handler) = Build();
        handler.Enqueue(
            FakeHttpHandler.Created(UploadJson("https://github.com/org/files/blob/main/feedback/2025/01/abc.png")));

        var att = new FeedbackAttachment("photo.png", PngBytes());
        await store.Upload(att);

        var req = handler.SentRequests[0].Request;
        req.RequestUri!.ToString().Should().MatchRegex(
            @"repos/org/files/contents/feedback/\d{4}/\d{2}/[0-9a-f\-]+\.png");
    }

    [Fact]
    public async Task Upload_body_contains_base64_and_branch()
    {
        var (store, handler) = Build();
        handler.Enqueue(
            FakeHttpHandler.Created(UploadJson("https://github.com/org/files/blob/main/feedback/2025/01/abc.png")));

        await store.Upload(new FeedbackAttachment("img.png", PngBytes()));

        var json = handler.SentRequests[0].Body;
        var doc = JsonDocument.Parse(json).RootElement;
        doc.GetProperty("message").GetString().Should().Be("Add feedback attachment");
        doc.GetProperty("branch").GetString().Should().Be("main");
        doc.GetProperty("content").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Upload_returns_html_url_with_raw_query()
    {
        var (store, handler) = Build();
        var htmlUrl = "https://github.com/org/files/blob/main/feedback/2025/01/abc.png";
        handler.Enqueue(FakeHttpHandler.Created(UploadJson(htmlUrl)));

        var result = await store.Upload(new FeedbackAttachment("img.png", PngBytes()));

        result.Url.Should().Be(htmlUrl + "?raw=true");
    }

    [Fact]
    public async Task Upload_builds_fallback_url_when_html_url_missing()
    {
        var (store, handler) = Build();
        handler.Enqueue(FakeHttpHandler.Created("{\"content\":{}}"));

        var result = await store.Upload(new FeedbackAttachment("img.png", PngBytes()));

        result.Url.Should().Contain("?raw=true");
        result.Url.Should().StartWith("https://github.com/org/files/blob/main/");
    }

    [Fact]
    public async Task Upload_sets_is_image_for_png()
    {
        var (store, handler) = Build();
        handler.Enqueue(FakeHttpHandler.Created(UploadJson("https://github.com/org/files/blob/main/x.png")));
        var result = await store.Upload(new FeedbackAttachment("img.png", PngBytes()));
        result.IsImage.Should().BeTrue();
    }

    [Fact]
    public async Task Retries_on_409_up_to_3_total_attempts()
    {
        var (store, handler) = Build();
        handler.Enqueue(FakeHttpHandler.Conflict());
        handler.Enqueue(FakeHttpHandler.Conflict());
        handler.Enqueue(FakeHttpHandler.Created(UploadJson("https://github.com/org/files/blob/main/x.png")));

        var result = await store.Upload(new FeedbackAttachment("img.png", PngBytes()));
        handler.SentRequests.Should().HaveCount(3);
        result.Url.Should().Contain("?raw=true");
    }

    [Fact]
    public async Task Throws_on_409_after_all_attempts()
    {
        var (store, handler) = Build();
        handler.Enqueue(FakeHttpHandler.Conflict());
        handler.Enqueue(FakeHttpHandler.Conflict());
        handler.Enqueue(FakeHttpHandler.Conflict());

        var act = () => store.Upload(new FeedbackAttachment("img.png", PngBytes()));
        await act.Should().ThrowAsync<GitHubApiException>();
        handler.SentRequests.Should().HaveCount(3);
    }

    [Fact]
    public async Task Retries_on_429_with_retry_after_header()
    {
        var (store, handler) = Build();
        handler.Enqueue(FakeHttpHandler.TooManyRequests(retryAfter: 1));
        handler.Enqueue(FakeHttpHandler.Created(UploadJson("https://github.com/org/files/blob/main/x.png")));

        var result = await store.Upload(new FeedbackAttachment("img.png", PngBytes()));
        handler.SentRequests.Should().HaveCount(2);
        result.Url.Should().Contain("?raw=true");
    }

    [Fact]
    public async Task Does_not_retry_on_404()
    {
        var (store, handler) = Build();
        handler.Enqueue(FakeHttpHandler.NotFound());

        var act = () => store.Upload(new FeedbackAttachment("img.png", PngBytes()));
        await act.Should().ThrowAsync<GitHubApiException>();
        handler.SentRequests.Should().HaveCount(1);
    }

    private static string UploadJson(string htmlUrl) =>
        $"{{\"content\":{{\"html_url\":\"{htmlUrl}\",\"path\":\"feedback/2025/01/abc.png\"}}}}";
}

/// <summary>
/// Factory that always returns one pre-wired client by name.
/// </summary>
internal sealed class SingleClientFactory : IHttpClientFactory
{
    private readonly HttpClient client;
    private readonly string name;

    public SingleClientFactory(HttpMessageHandler handler, string name)
    {
        client = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        this.name = name;
    }

    public HttpClient CreateClient(string clientName) =>
        clientName != name
            ? throw new InvalidOperationException($"Unexpected client name '{clientName}'.")
            : client;
}