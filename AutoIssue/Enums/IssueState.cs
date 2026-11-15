namespace AutoIssue.Enums;

/// <summary>
/// Specifies the target state when updating an issue.
/// </summary>
public enum IssueState
{
    /// <summary>
    /// Reopen the issue.
    /// </summary>
    Open,

    /// <summary>
    /// Close the issue.
    /// </summary>
    Closed,
}