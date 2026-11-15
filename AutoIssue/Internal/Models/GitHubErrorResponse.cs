using System.Text.Json.Serialization;

namespace AutoIssue.Internal.Models;

internal sealed class GitHubErrorResponse
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}