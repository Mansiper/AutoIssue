using System.Net;

namespace AutoIssue.Exceptions;

/// <summary>
/// Thrown when a GitHub API call returns a non-success HTTP status.
/// The <see cref="StatusCode"/> and <see cref="Exception.Message"/> carry the HTTP status and
/// the error description extracted from GitHub's JSON response.
/// </summary>
public sealed class GitHubApiException : Exception
{
    /// <summary>
    /// The HTTP status code returned by the GitHub API.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Initialises a new instance with the given <paramref name="statusCode"/> and <paramref name="message"/>.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="message">Human-readable error description.</param>
    public GitHubApiException(HttpStatusCode statusCode, string message)
        : base(message) =>
        StatusCode = statusCode;

    /// <summary>
    /// Initialises a new instance with an inner exception.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="message">Human-readable error description.</param>
    /// <param name="innerException">The underlying exception.</param>
    public GitHubApiException(HttpStatusCode statusCode, string message, Exception innerException)
        : base(message, innerException) =>
        StatusCode = statusCode;
}