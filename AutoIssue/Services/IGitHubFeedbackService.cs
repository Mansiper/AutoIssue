using AutoIssue.Enums;
using AutoIssue.Models;

namespace AutoIssue.Services;

/// <summary>
/// Main service for creating and querying feedback issues on GitHub.
/// Register via <c>AddGitHubFeedback(IConfiguration)</c>.
/// </summary>
public interface IGitHubFeedbackService
{
    /// <summary>
    /// Validates the request, uploads any attachments, creates a GitHub issue, and returns it.
    /// </summary>
    /// <param name="request">The feedback to submit. <see cref="Models.FeedbackRequest.Message"/> is required.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created issue.</returns>
    /// <exception cref="ArgumentException">Thrown for invalid input (empty message, oversized files, disallowed extensions, etc.).</exception>
    /// <exception cref="Exceptions.GitHubApiException">Thrown when the GitHub API returns a non-success response.</exception>
    Task<FeedbackIssue> CreateFeedback(
        FeedbackRequest request, CancellationToken ct = default);

    /// <summary>
    /// Returns all issues from the configured repository, paging through up to 100 pages.
    /// Pull requests are excluded.
    /// </summary>
    /// <param name="state">Filter by issue state.</param>
    /// <param name="feedbackOnly">When <see langword="true"/>, only issues with the configured feedback label are returned.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<FeedbackIssue>> GetIssues(
        IssueStateFilter state = IssueStateFilter.All,
        bool feedbackOnly = true,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the issue with the given <paramref name="number"/>, or <see langword="null"/> when it
    /// does not exist (404/410) or belongs to a pull request.
    /// </summary>
    /// <param name="number">The issue number.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<FeedbackIssue?> GetIssueByNumber(int number, CancellationToken ct = default);

    /// <summary>
    /// Opens or closes the issue identified by <paramref name="number"/>.
    /// </summary>
    /// <param name="number">The issue number.</param>
    /// <param name="state">The target state.</param>
    /// <param name="closeReason">
    /// Closing reason; ignored when <paramref name="state"/> is <see cref="IssueState.Open"/>.
    /// Defaults to <see cref="IssueCloseReason.Completed"/>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<FeedbackIssue> UpdateIssueStatus(
        int number,
        IssueState state,
        IssueCloseReason? closeReason = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all feedback issues where the contact matches <paramref name="contact"/> (case-insensitive).
    /// Filtering is done locally after listing all issues; this avoids GitHub search-API rate limits.
    /// </summary>
    /// <param name="contact">The contact string to match.</param>
    /// <param name="state">Filter by issue state.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<FeedbackIssue>> GetIssuesByContact(
        string contact,
        IssueStateFilter state = IssueStateFilter.All,
        CancellationToken ct = default);
}