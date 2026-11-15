using AutoIssue.Models;
using AutoIssue.Services;

namespace AutoIssue.Example;

public class CustomValidator : DefaultAttachmentFileValidator
{
    private readonly byte[] nefSignature = [.. "II*\0"u8]; // Example NEF signature (little-endian TIFF)

    public override void Validate(FeedbackAttachment attachment)
    {
        base.Validate(attachment);

        var ext = Path.GetExtension(attachment.FileName).ToLowerInvariant();

        if (ext == ".nef" && !StartsWith(attachment.Content, nefSignature)) 
            throw new ArgumentException("Attachment content does not match the expected format for NEF files.");
    }
}