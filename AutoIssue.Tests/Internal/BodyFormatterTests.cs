using AutoIssue.Internal;
using AutoIssue.Models;

namespace AutoIssue.Tests.Internal;

public sealed class BodyFormatterTests
{
    [Fact]
    public void Title_short_message_no_ellipsis()
    {
        var t = BodyFormatter.GenerateTitle("Hello world");
        t.Should().Be("Feedback: Hello world");
    }

    [Fact]
    public void Title_long_message_truncated_with_ellipsis()
    {
        var msg = new string('x', 80);
        var t = BodyFormatter.GenerateTitle(msg);
        t.Should().StartWith("Feedback: ");
        t.Should().EndWith("…");
        t.Length.Should().Be("Feedback: ".Length + 70 + 1); // +1 for '…'
    }

    [Fact]
    public void Title_collapses_whitespace()
    {
        var t = BodyFormatter.GenerateTitle("  Hello\n  World  ");
        t.Should().Be("Feedback: Hello World");
    }

    [Fact]
    public void Display_name_strips_directory()
    {
        var n = BodyFormatter.SanitizeDisplayName("some/path/file.png");
        n.Should().Be("file.png");
    }

    [Fact]
    public void Display_name_escapes_markdown_chars()
    {
        var n = BodyFormatter.SanitizeDisplayName("my[file](doc)!.pdf");
        n.Should().Contain(@"\[");
        n.Should().Contain(@"\]");
        n.Should().Contain(@"\(");
        n.Should().Contain(@"\)");
        n.Should().Contain(@"\!");
    }

    [Fact]
    public void Display_name_truncates_at_80_chars()
    {
        var name = new string('a', 100) + ".pdf";
        var n = BodyFormatter.SanitizeDisplayName(name);
        n.Length.Should().BeLessThanOrEqualTo(80);
    }

    [Fact]
    public void Build_body_contains_message()
    {
        var body = BodyFormatter.BuildBody("My feedback", null, [], []);
        body.Should().Contain("My feedback");
    }

    [Fact]
    public void Build_body_defuses_at_mentions()
    {
        var body = BodyFormatter.BuildBody("Hey @admin!", null, [], []);
        body.Should().Contain("@\u200B");
        body.Should().NotContain("@admin");
    }

    [Fact]
    public void Build_body_no_contact_shows_not_provided()
    {
        var body = BodyFormatter.BuildBody("msg", null, [], []);
        body.Should().Contain("**Contact:** _not provided_");
    }

    [Fact]
    public void Build_body_with_contact_normalizes_whitespace()
    {
        var body = BodyFormatter.BuildBody("msg", "  user@example.com  ", [], []);
        body.Should().Contain("**Contact:** user@example.com");
    }

    [Fact]
    public void Build_body_contact_capped_at_200_chars()
    {
        var longContact = new string('c', 250);
        var body = BodyFormatter.BuildBody("msg", longContact, [], []);
        const string prefix = "**Contact:** ";
        var idx = body.IndexOf(prefix, StringComparison.Ordinal);
        idx.Should().BeGreaterThanOrEqualTo(0);
        var afterPrefix = body[(idx + prefix.Length)..];
        var contactLine = afterPrefix.Split('\n')[0].TrimEnd('\r');
        contactLine.Length.Should().BeLessThanOrEqualTo(200);
    }

    [Fact]
    public void Build_body_image_uses_bang_prefix()
    {
        var atts = new List<AttachmentInfo> { new("shot.png", "https://example.com/shot.png", true) };
        var body = BodyFormatter.BuildBody("msg", null, atts, []);
        body.Should().Contain("![shot.png](https://example.com/shot.png)");
    }

    [Fact]
    public void Build_body_non_image_uses_plain_link()
    {
        var atts = new List<AttachmentInfo> { new("doc.pdf", "https://example.com/doc.pdf", false) };
        var body = BodyFormatter.BuildBody("msg", null, atts, []);
        body.Should().Contain("[doc.pdf](https://example.com/doc.pdf)");
        body.Should().NotContain("![doc.pdf]");
    }

    [Fact]
    public void Build_body_not_uploaded_section()
    {
        var body = BodyFormatter.BuildBody("msg", null, [], ["big.log"]);
        body.Should().Contain("**Not uploaded:**");
        body.Should().Contain("big.log");
    }

    [Fact]
    public void Build_body_no_attachments_section_when_empty()
    {
        var body = BodyFormatter.BuildBody("msg", null, [], []);
        body.Should().NotContain("**Attachments:**");
    }

    [Fact]
    public void Parse_round_trip_contact()
    {
        var body = BodyFormatter.BuildBody("Hello", "user@test.com", [], []);
        var (contact, _) = BodyFormatter.ParseBody(body);
        contact.Should().Be("user@test.com");
    }

    [Fact]
    public void Parse_no_contact_returns_null()
    {
        var body = BodyFormatter.BuildBody("Hello", null, [], []);
        var (contact, _) = BodyFormatter.ParseBody(body);
        contact.Should().BeNull();
    }

    [Fact]
    public void Parse_round_trip_attachments()
    {
        var atts = new List<AttachmentInfo>
        {
            new("img.png", "https://gh.com/img.png?raw=true", true),
            new("doc.pdf", "https://gh.com/doc.pdf?raw=true", false),
        };
        var body = BodyFormatter.BuildBody("Hello", null, atts, []);
        var (_, parsed) = BodyFormatter.ParseBody(body);
        parsed.Should().HaveCount(2);
        parsed[0].Name.Should().Be("img.png");
        parsed[0].IsImage.Should().BeTrue();
        parsed[1].Name.Should().Be("doc.pdf");
        parsed[1].IsImage.Should().BeFalse();
    }

    [Fact]
    public void Parse_spoofed_contact_uses_last_match()
    {
        var body = BodyFormatter.BuildBody("**Contact:** evil@hack.com\nMy real message", "real@example.com", [], []);
        var (contact, _) = BodyFormatter.ParseBody(body);
        contact.Should().Be("real@example.com");
    }

    [Fact]
    public void Parse_empty_body_returns_nulls()
    {
        var (contact, atts) = BodyFormatter.ParseBody(null);
        contact.Should().BeNull();
        atts.Should().BeEmpty();
    }

    [Fact]
    public void Parse_body_without_footer_returns_null_contact()
    {
        var (contact, atts) = BodyFormatter.ParseBody("Just some text without a footer.");
        contact.Should().BeNull();
        atts.Should().BeEmpty();
    }
}