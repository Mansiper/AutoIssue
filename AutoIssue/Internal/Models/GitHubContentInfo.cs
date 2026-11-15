using System.Text.Json.Serialization;

namespace AutoIssue.Internal.Models;

internal sealed class GitHubContentInfo
{
    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
}