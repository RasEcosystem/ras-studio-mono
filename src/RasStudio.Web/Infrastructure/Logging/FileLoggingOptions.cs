namespace RasStudio.Web.Infrastructure.Logging;

public sealed class FileLoggingOptions
{
    public const string SectionName = "FileLogging";
    public const int DefaultRetainedFileCountLimit = 14;
    public const long DefaultFileSizeLimitBytes = 20 * 1024 * 1024;

    public int RetainedFileCountLimit { get; init; } = DefaultRetainedFileCountLimit;

    public long FileSizeLimitBytes { get; init; } = DefaultFileSizeLimitBytes;

    public static FileLoggingOptions Load(IConfiguration configuration)
    {
        var options = configuration
            .GetSection(SectionName)
            .Get<FileLoggingOptions>() ?? new FileLoggingOptions();

        if (options.RetainedFileCountLimit is < 1 or > 365)
            throw new InvalidOperationException(
                "FileLogging:RetainedFileCountLimit must be between 1 and 365.");
        if (options.FileSizeLimitBytes is < 1_048_576 or > 1_073_741_824)
            throw new InvalidOperationException(
                "FileLogging:FileSizeLimitBytes must be between 1 MiB and 1 GiB.");

        return options;
    }
}
