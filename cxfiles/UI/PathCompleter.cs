namespace CXFiles.UI;

public static class PathCompleter
{
    public static (string parent, string fragment) SplitFragment(string input)
    {
        if (string.IsNullOrEmpty(input))
            return (string.Empty, string.Empty);

        int lastSep = input.LastIndexOfAny(new[] { '/', '\\' });
        if (lastSep < 0)
            return (string.Empty, input);

        var parent = input.Substring(0, lastSep);
        var fragment = input.Substring(lastSep + 1);

        if (parent.Length == 0)
            parent = Path.DirectorySeparatorChar.ToString();

        return (parent, fragment);
    }

    public static IReadOnlyList<string> Complete(string parent, string fragment, bool includeHidden)
    {
        if (!Directory.Exists(parent)) return Array.Empty<string>();

        var comparer = OperatingSystem.IsLinux()
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var results = new List<string>();
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(parent))
            {
                var name = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(name)) continue;
                if (!includeHidden && name.StartsWith('.')) continue;
                if (fragment.Length == 0 || name.StartsWith(fragment, comparer))
                    results.Add(name);
            }
        }
        catch { return Array.Empty<string>(); }

        results.Sort(OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        return results;
    }

    public static string LongestCommonPrefix(IReadOnlyList<string> items)
    {
        if (items.Count == 0) return string.Empty;
        var first = items[0];
        int prefixLen = first.Length;
        for (int i = 1; i < items.Count; i++)
        {
            var cur = items[i];
            int j = 0;
            int max = Math.Min(prefixLen, cur.Length);
            while (j < max && CharEqual(first[j], cur[j])) j++;
            prefixLen = j;
            if (prefixLen == 0) break;
        }
        return first.Substring(0, prefixLen);

        static bool CharEqual(char a, char b)
            => OperatingSystem.IsLinux() ? a == b : char.ToLowerInvariant(a) == char.ToLowerInvariant(b);
    }

    public static string Resolve(string input, string baseDir)
    {
        if (string.IsNullOrWhiteSpace(input)) return baseDir;

        if (input == "~" || input.StartsWith("~/") || input.StartsWith("~\\"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            input = input.Length == 1 ? home : Path.Combine(home, input.Substring(2));
        }

        if (Path.IsPathRooted(input))
            return Path.GetFullPath(input);

        return Path.GetFullPath(Path.Combine(baseDir, input));
    }
}
