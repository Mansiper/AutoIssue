using AutoIssue.Models;

namespace AutoIssue.Services;

/// <summary>
/// Validates individual attachment content beyond option-based checks (count, size, extension).
/// Register a custom implementation in the DI container to support additional file types or apply
/// extra content rules such as custom magic-byte checks or virus scanning.
/// </summary>
/// <example>
/// <code>
/// // In Program.cs, after AddGitHubFeedback:
/// builder.Services.AddScoped&lt;IAttachmentFileValidator, MyCustomValidator&gt;();
/// </code>
/// </example>
public interface IAttachmentFileValidator
{
    /// <summary>
    /// Validates the content of a single attachment.
    /// Throw <see cref="ArgumentException"/> to reject the file.
    /// </summary>
    /// <param name="attachment">The attachment to validate.</param>
    void Validate(FeedbackAttachment attachment);
}