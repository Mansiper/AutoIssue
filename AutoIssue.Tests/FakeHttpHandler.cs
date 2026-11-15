using System.Net;

namespace AutoIssue.Tests;

/// <summary>
/// A configurable fake HTTP handler for use in unit tests.
/// Eagerly reads and caches each request body so it stays readable after disposal.
/// </summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> responses = new();

    /// <summary>
    /// Captured (request, body-string) pairs in call order.
    /// </summary>
    public List<(HttpRequestMessage Request, string Body)> SentRequests { get; } = [];

    /// <summary>
    /// Enqueue a fixed response.
    /// </summary>
    public void Enqueue(HttpResponseMessage response) =>
        responses.Enqueue(_ => response);

    /// <summary>
    /// Enqueue a response factory that inspects the request.
    /// </summary>
    public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> factory) =>
        responses.Enqueue(factory);

    public static HttpResponseMessage Json(HttpStatusCode status, string json)
    {
        var r = new HttpResponseMessage(status);
        r.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return r;
    }

    public static HttpResponseMessage Ok(string json) => Json(HttpStatusCode.OK, json);
    public static HttpResponseMessage Created(string json) => Json(HttpStatusCode.Created, json);
    public static HttpResponseMessage NotFound() => new(HttpStatusCode.NotFound);
    public static HttpResponseMessage Gone() => new(HttpStatusCode.Gone);

    public static HttpResponseMessage Conflict(string msg = "Conflict")
        => Json(HttpStatusCode.Conflict, $"{{\"message\":\"{msg}\"}}");

    public static HttpResponseMessage TooManyRequests(int retryAfter = 0)
    {
        var r = Json(HttpStatusCode.TooManyRequests, "{\"message\":\"rate limited\"}");
        if (retryAfter > 0)
            r.Headers.Add("Retry-After", retryAfter.ToString());
        return r;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is not null
            ? await request.Content.ReadAsStringAsync(ct)
            : string.Empty;
        SentRequests.Add((request, body));

        return responses.Count == 0
            ? throw new InvalidOperationException("No more responses queued.")
            : responses.Dequeue()(request);
    }
}