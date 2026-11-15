using AutoIssue.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoIssue.Internal;

internal static class BodyFormatter
{
    private static readonly Regex ImageLinkRegex = new(
        @"^!\[([^\]]*)\]\(([^)]+)\)$", RegexOptions.Compiled);

    private static readonly Regex PlainLinkRegex = new(
        @"^\[([^\]]*)\]\(([^)]+)\)$", RegexOptions.Compiled);

    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".webp" };

    public static bool IsImageExtension(string ext) => ImageExtensions.Contains(ext);

    public static string BuildBody(
        string message,
        string? contact,
        IReadOnlyList<AttachmentInfo> attachments,
        IReadOnlyList<string> failedAttachmentNames)
    {
        var sb = new StringBuilder();
        sb.AppendLine(DefuseAtMentions(message.Trim()));
        sb.AppendLine();
        sb.AppendLine("---");
        sb.Append("**Contact:** ");
        sb.AppendLine(NormalizeContact(contact) ?? "_not provided_");

        if (attachments.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Attachments:**");
            foreach (var att in attachments)
                sb.AppendLine(att.IsImage
                    ? $"- ![{att.Name}]({att.Url})"
                    : $"- [{att.Name}]({att.Url})");
        }

        if (failedAttachmentNames.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Not uploaded:**");
            foreach (var name in failedAttachmentNames)
                sb.AppendLine($"- {SanitizeDisplayName(name)}");
        }

        return sb.ToString().TrimEnd();
    }

    public static (string? Contact, IReadOnlyList<AttachmentInfo> Attachments) ParseBody(string? body)
    {
        if (string.IsNullOrEmpty(body))
            return (null, []);

        var lines = body.ReplaceLineEndings("\n").Split('\n');

        // Contact — scan from the bottom so the footer's line wins over any
        // spoofed line the visitor may have written inside the message.
        string? contact = null;
        const string contactPrefix = "**Contact:** ";
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            if (lines[i].StartsWith(contactPrefix, StringComparison.Ordinal))
            {
                var val = lines[i][contactPrefix.Length..].Trim();
                contact = val == "_not provided_" ? null : val;
                break;
            }
        }

        // Attachments — last **Attachments:** section wins for the same reason.
        var attachmentsStart = -1;
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            if (lines[i].Trim() == "**Attachments:**")
            {
                attachmentsStart = i;
                break;
            }
        }

        var attachments = new List<AttachmentInfo>();
        if (attachmentsStart >= 0)
        {
            for (var i = attachmentsStart + 1; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                if (string.IsNullOrEmpty(trimmed)) break;
                if (!trimmed.StartsWith("- ", StringComparison.Ordinal)) break;

                var item = trimmed[2..];
                if (item.StartsWith("![", StringComparison.Ordinal))
                {
                    var m = ImageLinkRegex.Match(item);
                    if (m.Success)
                        attachments.Add(new AttachmentInfo(m.Groups[1].Value, m.Groups[2].Value, true));
                }
                else if (item.StartsWith("[", StringComparison.Ordinal))
                {
                    var m = PlainLinkRegex.Match(item);
                    if (m.Success)
                        attachments.Add(new AttachmentInfo(m.Groups[1].Value, m.Groups[2].Value, false));
                }
            }
        }

        return (contact, attachments);
    }

    public static string GenerateTitle(string message)
    {
        var collapsed = string.Join(" ", message.Split(
            ['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries));
        const int maxLen = 70;
        const string prefix = "Feedback: ";
        return prefix + (collapsed.Length > maxLen ? collapsed[..maxLen] + "…" : collapsed);
    }

    public static string SanitizeDisplayName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        name = string.Concat(name.Where(c => !char.IsControl(c)));
        name = name
            .Replace(@"\", @"\\")
            .Replace("[", @"\[")
            .Replace("]", @"\]")
            .Replace("(", @"\(")
            .Replace(")", @"\)")
            .Replace("!", @"\!");
        return name.Length > 80 ? name[..80] : name;
    }

    private static string DefuseAtMentions(string text) =>
        text.Replace("@", "@\u200B");

    private static string? NormalizeContact(string? contact)
    {
        if (string.IsNullOrWhiteSpace(contact)) return null;
        var normalized = string.Join(" ", contact.Split(
            ['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length > 200 ? normalized[..200] : normalized;
    }
}