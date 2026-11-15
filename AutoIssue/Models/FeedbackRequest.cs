using AutoIssue.Services;

namespace AutoIssue.Models;

/// <summary>
/// Input to <see cref="IGitHubFeedbackService.CreateFeedback"/>.
/// </summary>
/// <param name="Message">Visitor's feedback text. Required and must not exceed the configured maximum length.</param>
/// <param name="Contact">Optional contact information (e-mail, phone, handle). Stored in the issue body.</param>
/// <param name="Attachments">
/// Optional file attachments. Validated and uploaded before the issue is created.
/// Ignored when <c>Attachments.Repository</c> is not configured.
/// </param>
/// <param name="Title">
/// Optional issue title. When <see langword="null"/> or empty, a title is generated from the first 70 characters of <paramref name="Message"/>.
/// </param>
public sealed record FeedbackRequest(
    string Message,
    string? Contact = null,
    IReadOnlyList<FeedbackAttachment>? Attachments = null,
    string? Title = null);