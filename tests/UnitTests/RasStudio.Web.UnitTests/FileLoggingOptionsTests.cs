using Microsoft.Extensions.Configuration;
using RasStudio.Web.Infrastructure.Logging;

namespace RasStudio.Web.UnitTests;

public sealed class FileLoggingOptionsTests
{
    [Fact]
    public void Load_UsesDefaultsWhenSectionIsMissing()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = FileLoggingOptions.Load(configuration);

        Assert.Equal(FileLoggingOptions.DefaultRetainedFileCountLimit,
            options.RetainedFileCountLimit);
        Assert.Equal(FileLoggingOptions.DefaultFileSizeLimitBytes,
            options.FileSizeLimitBytes);
    }

    [Theory]
    [InlineData("FileLogging:RetainedFileCountLimit", "0")]
    [InlineData("FileLogging:RetainedFileCountLimit", "366")]
    [InlineData("FileLogging:FileSizeLimitBytes", "1048575")]
    [InlineData("FileLogging:FileSizeLimitBytes", "1073741825")]
    public void Load_RejectsUnsafeRetentionAndFileLimits(string key, string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            FileLoggingOptions.Load(configuration));
    }
}
