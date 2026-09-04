namespace RasStudio.Application.RasHub;

public static class RasHubCompatibility
{
    public const string MinimumSupportedVersion = "0.1.1";

    private static readonly Version MinimumVersion = new(0, 1, 1);

    public static bool IsSupported(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        var withoutBuildMetadata = version.Split('+', 2)[0];
        var separatorIndex = withoutBuildMetadata.IndexOf('-');
        var coreVersion = separatorIndex >= 0
            ? withoutBuildMetadata[..separatorIndex]
            : withoutBuildMetadata;

        if (!Version.TryParse(coreVersion, out var parsed))
            return false;

        return separatorIndex < 0
            ? parsed >= MinimumVersion
            : parsed > MinimumVersion;
    }
}
