using System.Text.Json;

namespace AutoIssue.Providers;

internal interface IGitHubHttpProvider
{
    JsonSerializerOptions JsonOptions { get; }
    Task<T> Send<T>(HttpClient client, HttpRequestMessage request, CancellationToken ct);
    Task<T?> SendNullable<T>(HttpClient client, HttpRequestMessage request, CancellationToken ct) where T : class;
    Task EnsureSuccess(HttpResponseMessage response, CancellationToken ct);
}