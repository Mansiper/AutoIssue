using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoIssue.Exceptions;
using AutoIssue.Internal.Models;

namespace AutoIssue.Providers;

internal sealed class GitHubHttpProvider : IGitHubHttpProvider
{
    public JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<T> Send<T>(
        HttpClient client, HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        await EnsureSuccess(response, ct).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false)
               ?? throw new GitHubApiException(HttpStatusCode.OK, "Empty response body.");
    }

    public async Task<T?> SendNullable<T>(
        HttpClient client, HttpRequestMessage request, CancellationToken ct) where T : class
    {
        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            return null;
        await EnsureSuccess(response, ct).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false);
    }

    public async Task EnsureSuccess(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        string msg;
        try
        {
            var err = await response.Content
                .ReadFromJsonAsync<GitHubErrorResponse>(cancellationToken: ct)
                .ConfigureAwait(false);
            msg = err?.Message ?? response.ReasonPhrase ?? response.StatusCode.ToString();
        }
        catch
        {
            msg = response.ReasonPhrase ?? response.StatusCode.ToString();
        }

        throw new GitHubApiException(
            response.StatusCode,
            $"GitHub API error {(int)response.StatusCode}: {msg}");
    }
}