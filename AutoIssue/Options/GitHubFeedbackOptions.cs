namespace AutoIssue.Options;

/// <summary>
/// Top-level configuration for the GitHub Feedback Bridge.
/// </summary>
public sealed class GitHubFeedbackOptions
{
    /// <summary>
    /// The configuration section name used to bind these options.
    /// </summary>
    public const string SectionName = "GitHubFeedback";

    /// <summary>
    /// GitHub REST API base URL. Default: <c>https://api.github.com/</c>.
    /// </summary>
    public string ApiBaseUrl { get; set; } = "https://api.github.com/";

    /// <summary>
    /// Value sent in the <c>X-GitHub-Api-Version</c> header. Default: <c>2022-11-28</c>.
    /// </summary>
    public string ApiVersion { get; set; } = "2022-11-28";

    /// <summary>
    /// Value sent in the <c>User-Agent</c> header. Default: <c>FeedbackBridge</c>.
    /// </summary>
    public string UserAgent { get; set; } = "FeedbackBridge";

    /// <summary>
    /// Settings for the GitHub repository that receives issues.
    /// </summary>
    public IssuesOptions Issues { get; set; } = new();

    /// <summary>
    /// Settings for the GitHub repository that stores attachments. Leave <see cref="AttachmentsOptions.Repository"/> empty to disable attachments.
    /// </summary>
    public AttachmentsOptions Attachments { get; set; } = new();
}