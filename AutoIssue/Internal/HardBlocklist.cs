namespace AutoIssue.Internal;

internal static class HardBlocklist
{
    private static readonly HashSet<string> Blocked = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".msi", ".bat", ".cmd", ".com", ".scr", ".ps1",
        ".vbs", ".js", ".mjs", ".jar", ".sh", ".html", ".htm", ".svg",
        ".php", ".py",
    };

    public static bool IsBlocked(string extension) => Blocked.Contains(extension);
}