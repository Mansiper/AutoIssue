namespace AutoIssue.Models;

/// <summary>
/// Metadata about a file successfully uploaded as an attachment.
/// </summary>
/// <param name="Name">Sanitised display name shown in the issue body.</param>
/// <param name="Url">Direct URL to the raw file content in the files repository.</param>
/// <param name="IsImage">
/// <see langword="true"/> when the file is an image that can be rendered inline
/// (extensions: <c>.png</c>, <c>.jpg</c>, <c>.jpeg</c>, <c>.gif</c>, <c>.webp</c>).
/// </param>
public sealed record AttachmentInfo(string Name, string Url, bool IsImage);