using AutoIssue.Internal;
using AutoIssue.Internal.Models;
using AutoIssue.Models;
using AutoIssue.Options;
using AutoIssue.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;

namespace AutoIssue.Services;

internal sealed class GitHubRepoAttachmentStore : IFeedbackAttachmentStore
{
    private readonly IHttpClientFactory factory;
    private readonly GitHubFeedbackOptions options;
    private readonly IGitHubHttpProvider ghProvider;
    private readonly ILogger<GitHubRepoAttachmentStore> logger;
    private const int MaxAttempts = 3;

    public GitHubRepoAttachmentStore(
        IHttpClientFactory factory,
        IOptions<GitHubFeedbackOptions> options,
        IGitHubHttpProvider ghProvider,
        ILogger<GitHubRepoAttachmentStore> logger)
    {
        this.factory = factory;
        this.options = options.Value;
        this.ghProvider = ghProvider;
        this.logger = logger;
    }

    public async Task<AttachmentInfo> Upload(FeedbackAttachment file, CancellationToken ct = default)
    {
        var opts = this.options.Attachments;
        var now = DateTimeOffset.UtcNow;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var path = $"{opts.Folder}/{now:yyyy}/{now:MM}/{Guid.NewGuid()}{ext}";
        var owner = opts.Owner;
        var repo = opts.Repository;
        var branch = opts.Branch;
        var url = $"repos/{owner}/{repo}/contents/{path}";
        var client = factory.CreateClient(HttpClientNames.Attachments);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, url);
            request.Content = JsonContent.Create(
                new
                {
                    Message = "Add feedback attachment", Content = Convert.ToBase64String(file.Content), Branch = branch
                },
                options: ghProvider.JsonOptions);

            using var response = await client.SendAsync(request, ct).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var upload = await response.Content
                    .ReadFromJsonAsync<GitHubUploadResponse>(ghProvider.JsonOptions, ct)
                    .ConfigureAwait(false);

                var htmlUrl = upload?.Content?.HtmlUrl;
                if (string.IsNullOrEmpty(htmlUrl))
                    htmlUrl = $"https://github.com/{owner}/{repo}/blob/{branch}/{path}";

                return new AttachmentInfo(
                    BodyFormatter.SanitizeDisplayName(file.FileName),
                    htmlUrl + "?raw=true",
                    BodyFormatter.IsImageExtension(ext));
            }

            if (attempt < MaxAttempts)
            {
                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    logger.LogWarning(
                        "Conflict uploading '{File}', retrying (attempt {A}/{Max}).",
                        file.FileName, attempt, MaxAttempts);
                    continue;
                }

                if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden)
                {
                    var wait = GetRetryAfterSeconds(response);
                    if (wait > 0)
                    {
                        logger.LogWarning(
                            "Rate limited uploading '{File}', waiting {Sec}s (attempt {A}/{Max}).",
                            file.FileName, wait, attempt, MaxAttempts);
                        await Task.Delay(TimeSpan.FromSeconds(wait), ct).ConfigureAwait(false);
                        continue;
                    }
                }
            }

            await ghProvider.EnsureSuccess(response, ct).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Unreachable.");
    }

    private static int GetRetryAfterSeconds(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
            return (int)Math.Ceiling(delta.TotalSeconds);
        if (response.Headers.RetryAfter?.Date is { } date)
            return Math.Max(0, (int)Math.Ceiling((date - DateTimeOffset.UtcNow).TotalSeconds));
        return 0;
    }
}