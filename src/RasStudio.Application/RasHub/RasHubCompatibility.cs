namespace RasStudio.Application.RasHub;

public static class RasHubCompatibility
{
    public const string MinimumSupportedVersion = "0.1.1";

    private static readonly Version MinimumVersion = new(0, 1, 1);

    public static bool IsSupported(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        var coreVersion = version.Split(['-', '+'], 2)[0];
        return Version.TryParse(coreVersion, out var parsed) &&
               parsed >= MinimumVersion;
    }
}
