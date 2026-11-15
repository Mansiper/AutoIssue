using AutoIssue.Internal;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace AutoIssue.Options;

internal sealed class GitHubFeedbackOptionsValidator : IValidateOptions<GitHubFeedbackOptions>
{
    private static readonly Regex ExtRegex = new(@"^\.[a-z0-9]+$", RegexOptions.Compiled);

    public ValidateOptionsResult Validate(string? name, GitHubFeedbackOptions options)
    {
        var errors = new List<string>();

        // Required issues fields
        if (string.IsNullOrWhiteSpace(options.Issues.Owner))
            errors.Add("Issues.Owner is required.");
        if (string.IsNullOrWhiteSpace(options.Issues.Repository))
            errors.Add("Issues.Repository is required.");
        if (string.IsNullOrWhiteSpace(options.Issues.FeedbackLabel))
            errors.Add("Issues.FeedbackLabel is required.");

        if (options.Issues.MaxMessageLength <= 0)
            errors.Add("Issues.MaxMessageLength must be a positive number.");

        if (options.Attachments.IsEnabled)
        {
            // Token: at least one must be present when attachments are on
            if (string.IsNullOrWhiteSpace(options.Issues.Token) &&
                string.IsNullOrWhiteSpace(options.Attachments.Token))
                errors.Add("At least one token (Issues.Token or Attachments.Token) is required.");

            if (string.IsNullOrWhiteSpace(options.Attachments.Branch))
                errors.Add("Attachments.Branch is required.");

            ValidateFolder(options, errors);
            ValidateAllowedFileTypes(options, errors);
            ValidateAttachmentLimits(options, errors);

            // Normalize: empty Owner → Issues.Owner
            if (string.IsNullOrWhiteSpace(options.Attachments.Owner))
                options.Attachments.Owner = options.Issues.Owner;
        }
        else
        {
            // Attachments disabled — only the issues token is needed
            if (string.IsNullOrWhiteSpace(options.Issues.Token))
                errors.Add("Issues.Token is required when attachments are not configured.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateFolder(GitHubFeedbackOptions opts, List<string> errors)
    {
        var folder = opts.Attachments.Folder?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(folder))
        {
            errors.Add("Attachments.Folder is required.");
            return;
        }
        if (folder.Contains('\\'))
        {
            errors.Add("Attachments.Folder must not contain backslashes.");
            return;
        }
        var normalized = folder.Trim('/');
        var segments = normalized.Split('/');
        if (segments.Any(s => s == ".." || s.Length == 0))
        {
            errors.Add("Attachments.Folder must not contain '..', leading/trailing '/', or empty segments.");
            return;
        }
        opts.Attachments.Folder = normalized;
    }

    private static void ValidateAllowedFileTypes(GitHubFeedbackOptions opts, List<string> errors)
    {
        var types = opts.Attachments.AllowedFileTypes;
        if (types.Length == 0)
        {
            errors.Add("Attachments.AllowedFileTypes must not be empty.");
            return;
        }

        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ext in types)
        {
            var lower = ext?.ToLowerInvariant() ?? string.Empty;
            if (!ExtRegex.IsMatch(lower))
            {
                errors.Add($"Attachments.AllowedFileTypes: '{ext}' is not a valid extension (must be like '.png').");
                continue;
            }
            if (HardBlocklist.IsBlocked(lower))
            {
                errors.Add($"Attachments.AllowedFileTypes: '{ext}' is on the hard blocklist and cannot be used.");
                continue;
            }
            normalized.Add(lower);
        }

        if (normalized.Count == 0)
        {
            errors.Add("Attachments.AllowedFileTypes: no valid, non-blocked extensions remain.");
            return;
        }

        opts.Attachments.AllowedFileTypes = [.. normalized];
    }

    private static void ValidateAttachmentLimits(GitHubFeedbackOptions opts, List<string> errors)
    {
        if (opts.Attachments.MaxFileSizeBytes <= 0)
            errors.Add("Attachments.MaxFileSizeBytes must be a positive number.");
        if (opts.Attachments.MaxTotalSizeBytes <= 0)
            errors.Add("Attachments.MaxTotalSizeBytes must be a positive number.");
        if (opts.Attachments is { MaxFileSizeBytes: > 0, MaxTotalSizeBytes: > 0 } && 
            opts.Attachments.MaxFileSizeBytes > opts.Attachments.MaxTotalSizeBytes)
            errors.Add("Attachments.MaxFileSizeBytes must be <= Attachments.MaxTotalSizeBytes.");
        if (opts.Attachments.MaxFilesPerFeedback is < 1 or > Constants.MaxFilesPerFeedbackUpperBound)
            errors.Add($"Attachments.MaxFilesPerFeedback must be between 1 and {Constants.MaxFilesPerFeedbackUpperBound}.");
    }
}