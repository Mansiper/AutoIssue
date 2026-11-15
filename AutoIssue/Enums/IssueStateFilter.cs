namespace AutoIssue.Enums;

/// <summary>
/// Filters issues by state when listing.
/// </summary>
public enum IssueStateFilter
{
    /// <summary>
    /// Return only open issues.
    /// </summary>
    Open,

    /// <summary>
    /// Return only closed issues.
    /// </summary>
    Closed,

    /// <summary>
    /// Return all issues regardless of state.
    /// </summary>
    All,
}