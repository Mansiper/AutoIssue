using AutoIssue.Models;
using AutoIssue.Options;
using AutoIssue.Services;

namespace AutoIssue.Internal;

internal static class AttachmentValidator
{
    public static void Validate(
        IReadOnlyList<FeedbackAttachment> attachments,
        AttachmentsOptions opts,
        IAttachmentFileValidator fileValidator)
    {
        if (attachments.Count > opts.MaxFilesPerFeedback)
            throw new ArgumentException(
                $"Too many attachments. Maximum allowed is {opts.MaxFilesPerFeedback}.",
                nameof(attachments));

        long totalSize = 0;
        foreach (var att in attachments)
        {
            if (att.Content is null || att.Content.Length == 0)
                throw new ArgumentException(
                    $"Attachment '{att.FileName}' has no content.", nameof(attachments));

            var ext = Path.GetExtension(att.FileName);
            if (string.IsNullOrEmpty(ext))
                throw new ArgumentException(
                    $"Attachment '{att.FileName}' has no file extension.", nameof(attachments));

            var lowerExt = ext.ToLowerInvariant();
            if (!opts.AllowedFileTypes.Contains(lowerExt, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException(
                    $"Attachment '{att.FileName}': file type '{ext}' is not allowed.", nameof(attachments));

            if (att.Content.Length > opts.MaxFileSizeBytes)
                throw new ArgumentException(
                    $"Attachment '{att.FileName}' exceeds the maximum file size of {opts.MaxFileSizeBytes} bytes.",
                    nameof(attachments));

            totalSize += att.Content.Length;
            if (totalSize > opts.MaxTotalSizeBytes)
                throw new ArgumentException(
                    $"Total attachment size exceeds the maximum of {opts.MaxTotalSizeBytes} bytes.",
                    nameof(attachments));

            fileValidator.Validate(att);
        }
    }
}