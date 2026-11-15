namespace AutoIssue.Options;

/// <summary>
/// Settings for attachment storage in a GitHub repository.
/// </summary>
public sealed class AttachmentsOptions
{
    /// <summary>
    /// Gets whether attachments are enabled.
    /// Attachments are considered enabled when <see cref="Repository"/> is set.
    /// When disabled, any attachments passed to the service are silently ignored.
    /// </summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(Repository);

    /// <summary>
    /// Owner of the files repository. Defaults to <see cref="IssuesOptions.Owner"/> when empty.
    /// </summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>
    /// Name of the files repository. Leave empty to disable attachment support entirely.
    /// </summary>
    public string Repository { get; set; } = string.Empty;

    /// <summary>
    /// Branch that receives attachment commits. Must already exist.
    /// </summary>
    public string Branch { get; set; } = "main";

    /// <summary>
    /// Folder prefix inside the files repository. Use a unique value per service sharing the same repo.
    /// </summary>
    public string Folder { get; set; } = string.Empty;

    /// <summary>
    /// GitHub token with <em>Contents: write</em> on the files repository.
    /// Defaults to <see cref="IssuesOptions.Token"/> when empty.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of attachments per submission. Range: 1–5.
    /// </summary>
    public int MaxFilesPerFeedback { get; set; } = 5;

    /// <summary>
    /// Maximum size in bytes for a single attachment. Default: 5 MB.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 5_242_880;

    /// <summary>
    /// Maximum total size in bytes across all attachments in one submission. Default: 15 MB.
    /// </summary>
    public long MaxTotalSizeBytes { get; set; } = 15_728_640;

    /// <summary>
    /// Allowed file extensions (e.g. <c>".png"</c>, <c>".pdf"</c>).
    /// Case-insensitive and deduplicated during validation.
    /// Extensions on the hard blocklist are always rejected regardless of this setting.
    /// </summary>
    public string[] AllowedFileTypes { get; set; } =
        [".png", ".jpg", ".jpeg", ".gif", ".webp", ".pdf", ".txt", ".log"];
}