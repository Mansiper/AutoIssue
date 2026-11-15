namespace AutoIssue.Models;

/// <summary>
/// Represents a GitHub issue created from visitor feedback, including parsed contact
/// information and attachment links extracted from the issue body.
/// </summary>
/// <param name="Number">Issue number within the repository (the <c>#123</c> shown on GitHub).</param>
/// <param name="Id">GitHub's global numeric issue id.</param>
/// <param name="Title">Issue title.</param>
/// <param name="Body">Raw markdown body, including the generated footer.</param>
/// <param name="State"><c>"open"</c> or <c>"closed"</c>.</param>
/// <param name="StateReason"><c>"completed"</c>, <c>"not_planned"</c>, <c>"reopened"</c>, or <see langword="null"/>.</param>
/// <param name="Contact">Contact parsed from the issue body; <see langword="null"/> when the visitor gave none.</param>
/// <param name="Attachments">Attachment links parsed from the issue body; empty when none were uploaded.</param>
/// <param name="Url">Browser URL of the issue on GitHub.</param>
/// <param name="Labels">Label names on the issue.</param>
/// <param name="CreatedAt">Time the issue was created.</param>
/// <param name="ClosedAt">Time the issue was closed; <see langword="null"/> while open.</param>
public sealed record FeedbackIssue(
    int Number,
    long Id,
    string Title,
    string Body,
    string State,
    string? StateReason,
    string? Contact,
    IReadOnlyList<AttachmentInfo> Attachments,
    string Url,
    IReadOnlyList<string> Labels,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt);