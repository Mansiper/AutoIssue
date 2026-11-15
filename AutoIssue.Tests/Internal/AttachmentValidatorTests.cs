using AutoIssue.Internal;
using AutoIssue.Models;
using AutoIssue.Services;

namespace AutoIssue.Tests.Internal;

public sealed class AttachmentValidatorTests
{
    private static readonly IAttachmentFileValidator DefaultValidator = new DefaultAttachmentFileValidator();

    private static AttachmentsOptions DefaultOpts() => new()
    {
        AllowedFileTypes = [".png", ".jpg", ".pdf", ".txt", ".log"],
        MaxFilesPerFeedback = 5,
        MaxFileSizeBytes = 1_000_000,
        MaxTotalSizeBytes = 3_000_000,
    };

    private static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static byte[] Pdf() => [.. "%PDF-1"u8];
    private static byte[] Text() => [.. "Hello world"u8];

    [Fact]
    public void Valid_single_png_passes()
    {
        var atts = new[] { new FeedbackAttachment("img.png", Png()) };
        var act = () => AttachmentValidator.Validate(atts, DefaultOpts(), DefaultValidator);
        act.Should().NotThrow();
    }

    [Fact]
    public void Too_many_files_throws()
    {
        var opts = DefaultOpts();
        opts.MaxFilesPerFeedback = 2;
        var atts = Enumerable.Range(0, 3)
            .Select(i => new FeedbackAttachment($"img{i}.png", Png()))
            .ToArray();
        var act = () => AttachmentValidator.Validate(atts, opts, DefaultValidator);
        act.Should().Throw<ArgumentException>().WithMessage("*Too many*");
    }

    [Fact]
    public void File_exceeds_max_size_throws()
    {
        var opts = DefaultOpts();
        opts.MaxFileSizeBytes = 4;
        var atts = new[] { new FeedbackAttachment("img.png", Png()) }; // Png() is 8 bytes
        var act = () => AttachmentValidator.Validate(atts, opts, DefaultValidator);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Total_size_exceeded_throws()
    {
        var opts = DefaultOpts();
        opts.MaxTotalSizeBytes = 10;
        var atts = new[]
        {
            new FeedbackAttachment("a.png", Png()),
            new FeedbackAttachment("b.pdf", Pdf()),
        };
        var act = () => AttachmentValidator.Validate(atts, opts, DefaultValidator);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Disallowed_extension_throws()
    {
        var atts = new[] { new FeedbackAttachment("file.zip", [(byte)'P', (byte)'K', 0x03, 0x04]) };
        var act = () => AttachmentValidator.Validate(atts, DefaultOpts(), DefaultValidator);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void No_extension_throws()
    {
        var atts = new[] { new FeedbackAttachment("noext", Png()) };
        var act = () => AttachmentValidator.Validate(atts, DefaultOpts(), DefaultValidator);
        act.Should().Throw<ArgumentException>().WithMessage("*no file extension*");
    }

    [Fact]
    public void Text_renamed_to_png_throws()
    {
        var atts = new[] { new FeedbackAttachment("bad.png", Text()) };
        var act = () => AttachmentValidator.Validate(atts, DefaultOpts(), DefaultValidator);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Png_bytes_renamed_to_jpg_throws()
    {
        var opts = new AttachmentsOptions
        {
            AllowedFileTypes = [".jpg"],
            MaxFilesPerFeedback = 5,
            MaxFileSizeBytes = 1_000_000,
            MaxTotalSizeBytes = 3_000_000,
        };
        var atts = new[] { new FeedbackAttachment("bad.jpg", Png()) };
        var act = () => AttachmentValidator.Validate(atts, opts, DefaultValidator);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Nul_byte_in_txt_throws()
    {
        var data = "Hi\0!"u8.ToArray();
        var atts = new[] { new FeedbackAttachment("file.txt", data) };
        var act = () => AttachmentValidator.Validate(atts, DefaultOpts(), DefaultValidator);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Valid_txt_passes()
    {
        var atts = new[] { new FeedbackAttachment("notes.txt", Text()) };
        var act = () => AttachmentValidator.Validate(atts, DefaultOpts(), DefaultValidator);
        act.Should().NotThrow();
    }

    [Fact]
    public void Valid_pdf_passes()
    {
        var atts = new[] { new FeedbackAttachment("report.pdf", Pdf()) };
        var act = () => AttachmentValidator.Validate(atts, DefaultOpts(), DefaultValidator);
        act.Should().NotThrow();
    }

    [Fact]
    public void Empty_content_throws()
    {
        var atts = new[] { new FeedbackAttachment("img.png", []) };
        var act = () => AttachmentValidator.Validate(atts, DefaultOpts(), DefaultValidator);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Valid_webp_passes()
    {
        var opts = DefaultOpts();
        opts.AllowedFileTypes = [".webp"];
        var data = new byte[12];
        data[0] = (byte)'R';
        data[1] = (byte)'I';
        data[2] = (byte)'F';
        data[3] = (byte)'F';
        data[8] = (byte)'W';
        data[9] = (byte)'E';
        data[10] = (byte)'B';
        data[11] = (byte)'P';
        var atts = new[] { new FeedbackAttachment("img.webp", data) };
        var act = () => AttachmentValidator.Validate(atts, opts, DefaultValidator);
        act.Should().NotThrow();
    }

    [Fact]
    public void Custom_validator_called_for_each_attachment()
    {
        var called = new List<string>();
        var customValidator = new ActionValidator(att => called.Add(att.FileName));

        var atts = new[]
        {
            new FeedbackAttachment("a.png", Png()),
            new FeedbackAttachment("b.pdf", Pdf()),
        };
        AttachmentValidator.Validate(atts, DefaultOpts(), customValidator);

        called.Should().Equal("a.png", "b.pdf");
    }

    [Fact]
    public void Custom_validator_rejection_propagates()
    {
        var customValidator = new ActionValidator(att =>
        {
            if (att.FileName.EndsWith(".pdf"))
                throw new ArgumentException("PDF not allowed by custom rule.");
        });

        var atts = new[] { new FeedbackAttachment("doc.pdf", Pdf()) };
        var act = () => AttachmentValidator.Validate(atts, DefaultOpts(), customValidator);
        act.Should().Throw<ArgumentException>().WithMessage("*PDF not allowed*");
    }

    private sealed class ActionValidator(Action<FeedbackAttachment> validate) : IAttachmentFileValidator
    {
        public void Validate(FeedbackAttachment attachment) => validate(attachment);
    }
}