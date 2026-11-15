namespace AutoIssue.Options;

/// <summary>
/// Settings for the GitHub repository that receives feedback issues.
/// </summary>
public sealed class IssuesOptions
{
    /// <summary>
    /// Owner (user or organisation) of the issues repository. Required.
    /// </summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>
    /// Name of the issues repository. Required.
    /// </summary>
    public string Repository { get; set; } = string.Empty;

    /// <summary>
    /// GitHub token with <em>Issues: write</em> on the issues repository. Required when attachments are not configured.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Label added to every created issue and used as a filter when listing. Default: <c>"feedback"</c>.
    /// </summary>
    public string FeedbackLabel { get; set; } = "feedback";

    /// <summary>
    /// Extra labels added to every created issue in addition to <see cref="FeedbackLabel"/>.
    /// </summary>
    public string[] AdditionalLabels { get; set; } = [];

    /// <summary>
    /// Maximum allowed length of the feedback message in characters. Default: 5000.
    /// </summary>
    public int MaxMessageLength { get; set; } = 5000;
}