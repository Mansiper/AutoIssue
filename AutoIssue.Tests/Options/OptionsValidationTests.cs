namespace AutoIssue.Tests.Options;

public sealed class OptionsValidationTests
{
    private static GitHubFeedbackOptions Valid() => new()
    {
        Issues = new IssuesOptions
        {
            Owner = "org",
            Repository = "issues-repo",
            Token = "ghp_token",
            FeedbackLabel = "feedback",
        },
        Attachments = new AttachmentsOptions
        {
            Repository = "files-repo",
            Branch = "main",
            Folder = "website-feedback",
            AllowedFileTypes = [".png", ".pdf"],
            MaxFileSizeBytes = 1_000_000,
            MaxTotalSizeBytes = 5_000_000,
        },
    };

    private static GitHubFeedbackOptions ValidNoAttachments() => new()
    {
        Issues = new IssuesOptions
        {
            Owner = "org",
            Repository = "issues-repo",
            Token = "ghp_token",
            FeedbackLabel = "feedback",
        },
        // Attachments.Repository is empty → attachments disabled
    };

    private static ValidateOptionsResult Run(GitHubFeedbackOptions opts)
    {
        var v = new GitHubFeedbackOptionsValidator();
        return v.Validate(null, opts);
    }

    [Fact]
    public void Valid_config_passes() => Run(Valid()).Succeeded.Should().BeTrue();

    [Fact]
    public void Missing_owner_fails()
    {
        var o = Valid();
        o.Issues.Owner = "";
        Run(o).Succeeded.Should().BeFalse();
    }

    [Fact]
    public void Missing_repository_fails()
    {
        var o = Valid();
        o.Issues.Repository = "";
        Run(o).Succeeded.Should().BeFalse();
    }

    [Fact]
    public void Missing_feedback_label_fails()
    {
        var o = Valid();
        o.Issues.FeedbackLabel = "   ";
        Run(o).Succeeded.Should().BeFalse();
    }

    [Fact]
    public void No_token_at_all_fails()
    {
        var o = Valid();
        o.Issues.Token = "";
        o.Attachments.Token = "";
        Run(o).Succeeded.Should().BeFalse();
    }

    [Fact]
    public void Attachment_token_alone_is_sufficient()
    {
        var o = Valid();
        o.Issues.Token = "";
        o.Attachments.Token = "ghp_att";
        Run(o).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Missing_attachments_repository_fails()
    {
        var o = Valid();
        o.Attachments.Repository = "";
        // Repository empty = attachments disabled; Issues.Token must be present
        // (it is in Valid()) so this now PASSES (attachments optional)
        Run(o).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Missing_branch_fails()
    {
        var o = Valid();
        o.Attachments.Branch = "";
        Run(o).Succeeded.Should().BeFalse();
    }

    [Fact]
    public void Missing_folder_fails()
    {
        var o = Valid();
        o.Attachments.Folder = "";
        Run(o).Succeeded.Should().BeFalse();
    }

    [Fact]
    public void Folder_with_backslash_fails()
    {
        var o = Valid();
        o.Attachments.Folder = @"some\folder";
        Run(o).Succeeded.Should().BeFalse();
    }

    [Fact]
    public void Folder_with_dotdot_fails()
    {
        var o = Valid();
        o.Attachments.Folder = "parent/../other";
        Run(o).Succeeded.Should().BeFalse();
    }

    [Fact]
    public void Folder_leading_slash_is_normalized()
    {
        var o = Valid();
        o.Attachments.Folder = "/my-folder";
        Run(o).Succeeded.Should().BeTrue();
        o.Attachments.Folder.Should().Be("my-folder");
    }

    [Fact]
    public void Folder_trailing_slash_is_normalized()
    {
        var o = Valid();
        o.Attachments.Folder = "my-folder/";
        Run(o).Succeeded.Should().BeTrue();
        o.Attachments.Folder.Should().Be("my-folder");
    }

    [Fact]
    public void Empty_allowed_types_fails()
    {
        var o = Valid();
        o.Attachments.AllowedFileTypes = [];
        Run(o).Succeeded.Should().BeFalse();
    }

    [Fact]
    public void Extension_without_dot_fails()
    {
        var o = Valid();
        o.Attachments.AllowedFileTypes = ["png"];
        Run(o).Succeeded.Should().BeFalse();
    }

    [Fact]
    public void Wildcard_extension_fails()
    {
        var o = Valid();
        o.Attachments.AllowedFileTypes = [".*"];
        Run(o).Succeeded.Should().BeFalse();
    }

    [Fact]
    public void Blocked_extension_exe_fails()
    {
        var o = Valid();
        o.Attachments.AllowedFileTypes = [".png", ".exe"];
        var r = Run(o);
        r.Succeeded.Should().BeFalse();
        r.Failures!.Should().Contain(f => f.Contains(".exe"));
    }

    [Fact]
    public void Blocked_extension_js_fails()
    {
        var o = Valid();
        o.Attachments.AllowedFileTypes = [".js"];
        Run(o).Succeeded.Should().BeFalse();
    }

    [Fact]
    public void Duplicate_extensions_are_deduplicated()
    {
        var o = Valid();
        o.Attachments.AllowedFileTypes = [".png", ".PNG", ".pdf"];
        Run(o).Succeeded.Should().BeTrue();
        o.Attachments.AllowedFileTypes.Should().HaveCount(2);
    }

    [Fact]
    public void Zero_max_file_size_fails()
    {
        var o = Valid();
        o.Attachments.MaxFileSizeBytes = 0;
        Run(o).Succeeded.Should().BeFalse();
    }

    [Fact]
    public void File_size_exceeds_total_size_fails()
    {
        var o = Valid();
        o.Attachments.MaxFileSizeBytes = 10_000_000;
        o.Attachments.MaxTotalSizeBytes = 5_000_000;
        Run(o).Succeeded.Should().BeFalse();
    }

    [Fact]
    public void Max_files_out_of_range_fails()
    {
        var o = Valid();
        o.Attachments.MaxFilesPerFeedback = 6;
        Run(o).Succeeded.Should().BeFalse();
    }

    [Fact]
    public void Zero_max_message_length_fails()
    {
        var o = Valid();
        o.Issues.MaxMessageLength = 0;
        Run(o).Succeeded.Should().BeFalse();
    }

    [Fact]
    public void Empty_attachments_owner_defaults_to_issues_owner()
    {
        var o = Valid();
        o.Attachments.Owner = "";
        Run(o);
        o.Attachments.Owner.Should().Be(o.Issues.Owner);
    }

    // --- Optional-attachment scenarios ---

    [Fact]
    public void No_attachments_config_passes_when_issues_token_present()
    {
        var r = Run(ValidNoAttachments());
        r.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void No_attachments_config_without_issues_token_fails()
    {
        var o = ValidNoAttachments();
        o.Issues.Token = "";
        Run(o).Succeeded.Should().BeFalse();
    }

    [Fact]
    public void No_attachments_config_does_not_validate_attachment_fields()
    {
        // Branch / Folder / AllowedFileTypes are invalid but should be ignored
        // when Repository is empty.
        var o = ValidNoAttachments();
        o.Attachments.Branch = "";
        o.Attachments.Folder = "";
        o.Attachments.AllowedFileTypes = [];
        Run(o).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Attachments_IsEnabled_false_when_repository_empty()
    {
        var o = ValidNoAttachments();
        o.Attachments.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Attachments_IsEnabled_true_when_repository_set()
    {
        var o = Valid();
        o.Attachments.IsEnabled.Should().BeTrue();
    }
}