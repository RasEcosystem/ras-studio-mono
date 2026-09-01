namespace RasStudio.Web;

internal static class RasStudioVersion
{
    public const string Informational = ThisAssembly.AssemblyInformationalVersion;

    public static string Display { get; } = GetDisplayVersion(
        ThisAssembly.NuGetPackageVersion);

    public static string? PrereleaseLabel { get; } = GetPrereleaseLabel(Display);

    internal static string? GetPrereleaseLabel(string version)
    {
        var prereleaseIndex = version.IndexOf('-', StringComparison.Ordinal);

        if (prereleaseIndex < 0 || prereleaseIndex == version.Length - 1) return null;

        var labelStart = prereleaseIndex + 1;
        var labelEnd = version.IndexOf('.', labelStart);
        var label = labelEnd < 0
            ? version[labelStart..]
            : version[labelStart..labelEnd];

        return label.ToUpperInvariant();
    }

    internal static string GetDisplayVersion(string version)
    {
        var gitSuffixIndex = FindGitSuffix(version, ".g");

        if (gitSuffixIndex < 0)
            gitSuffixIndex = FindGitSuffix(version, "-g");

        return gitSuffixIndex < 0 ? version : version[..gitSuffixIndex];
    }

    private static int FindGitSuffix(string version, string marker)
    {
        var gitSuffixIndex = version.LastIndexOf(marker, StringComparison.Ordinal);

        if (gitSuffixIndex < 0 || gitSuffixIndex == version.Length - marker.Length)
            return -1;

        var gitRevision = version.AsSpan(gitSuffixIndex + marker.Length);

        foreach (var character in gitRevision)
            if (!Uri.IsHexDigit(character))
                return -1;

        return gitSuffixIndex;
    }
}
