using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RasStudio.Web.Infrastructure.Mcp;

namespace RasStudio.Web.UnitTests.Mcp;

public sealed class RasMcpServiceCollectionExtensionsTests
{
    [Theory]
    [InlineData(" ", "0.1.0", "Description")]
    [InlineData("RasStudio", " ", "Description")]
    [InlineData("RasStudio", "0.1.0", " ")]
    public void AddRasMcpRejectsIncompleteMetadata(
        string name,
        string version,
        string description)
    {
        var services = new ServiceCollection();
        services.AddRasMcp(options =>
        {
            options.Name = name;
            options.Version = version;
            options.Description = description;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RasMcpOptions>>();

        Assert.Throws<OptionsValidationException>(() => options.Value);
    }
}
