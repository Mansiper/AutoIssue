namespace AutoIssue.Models;

/// <summary>
/// A single file submitted by the visitor as part of a feedback request.
/// </summary>
/// <param name="FileName">
/// Original file name including extension. The extension determines the allowed type and
/// content-validation rules. The name itself is never used as the storage path.
/// </param>
/// <param name="Content">Raw byte content of the file.</param>
public sealed record FeedbackAttachment(string FileName, byte[] Content);