using System.Text.Json.Serialization;

namespace AutoIssue.Internal.Models;

internal sealed class GitHubUploadResponse
{
    [JsonPropertyName("content")]
    public GitHubContentInfo? Content { get; set; }
}