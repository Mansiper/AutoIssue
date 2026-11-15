using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoIssue.Internal.Models;

internal sealed class GitHubIssueResponse
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("state_reason")]
    public string? StateReason { get; set; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("labels")]
    public JsonElement[] Labels { get; set; } = [];

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("closed_at")]
    public DateTimeOffset? ClosedAt { get; set; }

    [JsonPropertyName("pull_request")]
    public JsonElement? PullRequest { get; set; }
}