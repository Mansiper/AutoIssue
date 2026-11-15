namespace AutoIssue.Enums;

/// <summary>
/// Specifies the reason for closing a feedback issue.
/// </summary>
public enum IssueCloseReason
{
    /// <summary>
    /// The issue was resolved or the work was completed.
    /// </summary>
    Completed,

    /// <summary>
    /// The issue will not be acted on.
    /// </summary>
    NotPlanned,
}