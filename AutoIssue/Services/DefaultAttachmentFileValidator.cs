using AutoIssue.Models;

namespace AutoIssue.Services;

/// <summary>
/// Default implementation of <see cref="IAttachmentFileValidator"/> that validates file content
/// using magic-byte signatures for common file types.
/// </summary>
public class DefaultAttachmentFileValidator : IAttachmentFileValidator
{
    /// <inheritdoc />
    public virtual void Validate(FeedbackAttachment attachment)
    {
        var ext  = Path.GetExtension(attachment.FileName).ToLowerInvariant();
        var data = attachment.Content;

        var valid = ext switch
        {
            ".png"  => StartsWith(data, 0x89, 0x50, 0x4E, 0x47),
            ".jpg" or ".jpeg" => StartsWith(data, 0xFF, 0xD8, 0xFF),
            ".gif"  => StartsWithText(data, "GIF8"),
            ".webp" => data.Length >= 12 &&
                       StartsWith(data, [.. "RIFF"u8]) &&
                       StartsWithAt(data, 8, [.. "WEBP"u8]),
            ".pdf"  => StartsWithText(data, "%PDF-"),
            ".zip" or ".docx" or ".xlsx" or ".pptx" => StartsWith(data, [.. "PK"u8]),
            ".txt" or ".log" or ".csv" or ".json" or ".md" => !data.Contains((byte)0),
            _ => true,
        };

        if (!valid)
            throw new ArgumentException(
                $"Attachment '{attachment.FileName}' content does not match the expected format for '{ext}'.");
    }

    protected static bool StartsWith(byte[] data, params byte[] sig)
    {
        if (data.Length < sig.Length) return false;
        return !sig.Where((t, i) => data[i] != t).Any();
    }

    protected static bool StartsWithAt(byte[] data, int offset, params byte[] sig)
    {
        if (data.Length < offset + sig.Length) return false;
        return !sig.Where((t, i) => data[offset + i] != t).Any();
    }

    protected static bool StartsWithText(byte[] data, string text)
    {
        if (data.Length < text.Length) return false;
        return !text.Where((t, i) => data[i] != (byte)t).Any();
    }
}