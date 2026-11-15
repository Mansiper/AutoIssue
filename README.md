[![NuGet Version](https://img.shields.io/nuget/v/Mansiper.AutoIssue)](https://www.nuget.org/packages/Mansiper.AutoIssue)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Mansiper.AutoIssue)](https://www.nuget.org/packages/Mansiper.AutoIssue)
[![License](https://img.shields.io/github/license/Mansiper/AutoIssue)](LICENSE)

# AutoIssue — GitHub Feedback Bridge

A .NET class library that turns website or app feedback into **GitHub issues**, with optional file attachments.

Visitors submit a message (and an optional contact + files). The library posts to GitHub using a server-side token, so no GitHub account is required on the visitor's side.

---

## Quick start

### 1. Create the repositories

| Repo | Purpose |
|---|---|
| **Issues repo** | Receives one issue per feedback submission. Any existing repository works. |
| **Files repo** | Stores uploaded attachments as committed files. It must have at least one commit on the target branch. Only needed when you want attachment support. |

### 2. Create a GitHub token

Create a **fine-grained personal access token** (ideally owned by a bot account) with:

- *Issues: Read and write* on the issues repository.
- *Contents: Read and write* on the files repository (only when using attachments).

GitHub -> Settings > Developer settings > Personal access tokens > Fine-grained tokens > Generate new token.  
Or just open this link: [https://github.com/settings/personal-access-tokens](https://github.com/settings/personal-access-tokens)

One token can cover both repositories. Store it in user-secrets or an environment variable — **never in source code**.

```
GitHubFeedback__Issues__Token=ghp_xxx
```

### 3. Install the package

```
dotnet add package Mansiper.AutoIssue
```

### 4. Configure `appsettings.json`

```json
{
  "GitHubFeedback": {
    "ApiBaseUrl": "https://api.github.com/",
    "ApiVersion": "2022-11-28",
    "UserAgent": "MyApp",

    "Issues": {
      "Owner": "your-org-or-user",
      "Repository": "feedback-issues",
      "Token": "",
      "FeedbackLabel": "feedback",
      "AdditionalLabels": [ "from-website" ],
      "MaxMessageLength": 5000
    },

    "Attachments": {
      "Owner": "",
      "Repository": "feedback-files",
      "Branch": "main",
      "Folder": "my-site-feedback",
      "Token": "",
      "MaxFilesPerFeedback": 5,
      "MaxFileSizeBytes": 5242880,
      "MaxTotalSizeBytes": 15728640,
      "AllowedFileTypes": [ ".png", ".jpg", ".jpeg", ".gif", ".webp", ".pdf", ".txt", ".log" ]
    }
  }
}
```

> `Attachments.Owner` defaults to `Issues.Owner` when left empty.  
> `Attachments.Token` defaults to `Issues.Token` when left empty.  
> Pick a **unique `Folder`** for each service that shares the files repository.  
> Leave `Attachments.Repository` **empty** to disable attachment support entirely.

### 5. Register the services

```csharp
builder.Services.AddGitHubFeedback(builder.Configuration);
```

Pass `validateOnStart: false` to defer configuration validation until first use (useful during development with placeholder credentials).

### 6. Submit feedback

```csharp
public class FeedbackController : ControllerBase
{
    private readonly IGitHubFeedbackService _feedback;
    public FeedbackController(IGitHubFeedbackService feedback) => _feedback = feedback;

    [HttpPost]
    public async Task<IActionResult> Submit([FromForm] FeedbackForm form, IFormFileCollection files)
    {
        var attachments = new List<FeedbackAttachment>();
        foreach (var file in files)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            attachments.Add(new FeedbackAttachment(file.FileName, ms.ToArray()));
        }

        var issue = await _feedback.CreateFeedback(new FeedbackRequest(
            Message: form.Message,
            Contact: form.Email,
            Attachments: attachments));

        return Ok(new { issue.Number, issue.Url });
    }
}
```

---

## Public API

### `IGitHubFeedbackService`

| Method | Description |
|---|---|
| `CreateFeedback(request)` | Validates input, uploads attachments (when enabled), creates the issue. Throws `ArgumentException` for invalid input, `GitHubApiException` on GitHub errors. |
| `GetIssues(state, feedbackOnly)` | Lists all issues (all pages). With `feedbackOnly = true` only issues with the feedback label are returned. Pull requests are excluded. |
| `GetIssueByNumber(number)` | Returns `null` for 404 / 410 and for pull-request numbers. |
| `UpdateIssueStatus(number, state, closeReason)` | Opens or closes an issue. Close reason is `Completed` (default) or `NotPlanned`. |
| `GetIssuesByContact(contact, state)` | Case-insensitive match on the stored contact. Filters locally (no search-API rate limits). |

### `IFeedbackAttachmentStore`

Can be replaced with a custom implementation (Azure Blob, S3, etc.) without touching the issue service:

```csharp
services.AddScoped<IFeedbackAttachmentStore, MyCustomStore>();
```

### `IAttachmentFileValidator`

Validates individual attachment content beyond the option-based checks (count, size, extension).
Replace the default magic-byte validator with your own to support proprietary file types or add extra content rules:

```csharp
// Register after AddGitHubFeedback so it overrides the default:
services.AddScoped<IAttachmentFileValidator, MyFileValidator>();

public class MyFileValidator : IAttachmentFileValidator
{
    public void Validate(FeedbackAttachment attachment)
    {
        // Throw ArgumentException to reject the file.
        if (attachment.FileName.EndsWith(".myapp") && !IsValidMyAppFile(attachment.Content))
            throw new ArgumentException($"'{attachment.FileName}' is not a valid .myapp file.");
    }
}
```
You can also inherit from `DefaultAttachmentFileValidator` and override only the `Validate` method.

### Models

```csharp
record FeedbackRequest(string Message, string? Contact, IReadOnlyList<FeedbackAttachment>? Attachments, string? Title);
record FeedbackAttachment(string FileName, byte[] Content);
record FeedbackIssue(int Number, long Id, string Title, string Body, string State, string? StateReason,
                     string? Contact, IReadOnlyList<AttachmentInfo> Attachments, string Url,
                     IReadOnlyList<string> Labels, DateTimeOffset CreatedAt, DateTimeOffset? ClosedAt);
record AttachmentInfo(string Name, string Url, bool IsImage);
```

---

## Attachment rules

Attachments are **optional**. Leave `Attachments.Repository` empty to disable them entirely; in that case any `Attachments` passed to `CreateFeedback` are silently ignored and no attachment-related configuration is required.

When enabled:

- Maximum **5 files** per submission (configurable, range 1–5).
- Per-file and total-size limits are configurable.
- Allowed extensions are **explicitly listed**; wildcards are rejected.
- The following extensions are always blocked regardless of configuration:  
  `.exe .dll .msi .bat .cmd .com .scr .ps1 .vbs .js .mjs .jar .sh .html .htm .svg .php .py`
- File content is validated against magic bytes / absence of NUL bytes for known extensions. Plug in `IAttachmentFileValidator` to extend this behaviour for custom file types.

Files are stored at `{Folder}/{yyyy}/{MM}/{guid}{ext}` in the files repository. The original file name is **never** used as the path, only as a display label.

---

## Configuration reference

| Key | Default | Description |
|---|---|---|
| `ApiBaseUrl` | `https://api.github.com/` | GitHub API base URL. |
| `ApiVersion` | `2022-11-28` | `X-GitHub-Api-Version` header value. |
| `UserAgent` | `FeedbackBridge` | `User-Agent` header sent to GitHub. |
| `Issues.Owner` | — | **Required.** Owner of the issues repository. |
| `Issues.Repository` | — | **Required.** Issues repository name. |
| `Issues.Token` | — | GitHub token with *Issues: write* on the issues repo. Required when attachments are not configured. |
| `Issues.FeedbackLabel` | `feedback` | Label added to every created issue; also the list filter. |
| `Issues.AdditionalLabels` | `[]` | Extra labels added to every created issue. |
| `Issues.MaxMessageLength` | `5000` | Maximum message length in characters. |
| `Attachments.Repository` | — | Files repository name. **Leave empty to disable attachments.** |
| `Attachments.Owner` | _(Issues.Owner)_ | Owner of the files repository. |
| `Attachments.Branch` | `main` | Branch that receives attachment commits. Must already exist. |
| `Attachments.Folder` | — | Folder prefix inside the files repo. Use a unique value per service. |
| `Attachments.Token` | _(Issues.Token)_ | Token with *Contents: write* on the files repo. |
| `Attachments.MaxFilesPerFeedback` | `5` | 1–5. |
| `Attachments.MaxFileSizeBytes` | `5242880` | 5 MB per file. |
| `Attachments.MaxTotalSizeBytes` | `15728640` | 15 MB per submission. |
| `Attachments.AllowedFileTypes` | see above | List of `.ext` values, case-insensitive, deduplicated. |

---

## Example

The `AutoIssue.Example` project is a Blazor Web App with working Feedback pages.  
To try it:

1. Fill in `Issues.Owner`, `Issues.Repository`, and `Issues.Token` in `AutoIssue.Example/appsettings.json`.
2. Optionally configure the `Attachments` section.
3. Run `dotnet run --project AutoIssue.Example` and navigate to `/feedback`.

---

## License

MIT
