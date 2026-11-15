using AutoIssue.Models;

namespace AutoIssue.Services;

/// <summary>
/// Stores a single attachment file and returns its public URL.
/// Replace the default implementation (<c>GitHubRepoAttachmentStore</c>) with a custom one
/// (e.g. Azure Blob Storage, Amazon S3) by registering it in the DI container before or after
/// calling <c>AddGitHubFeedback</c>:
/// <code>services.AddScoped&lt;IFeedbackAttachmentStore, MyCustomStore&gt;();</code>
/// </summary>
public interface IFeedbackAttachmentStore
{
    /// <summary>
    /// Uploads <paramref name="file"/> to the backing store and returns its public metadata.
    /// Throws <see cref="Exceptions.GitHubApiException"/> (or store-specific exceptions) on failure.
    /// </summary>
    /// <param name="file">The attachment to upload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Metadata including the public URL and display name of the stored file.</returns>
    Task<AttachmentInfo> Upload(FeedbackAttachment file, CancellationToken ct = default);
}