using ModelContextProtocol.Protocol;
using RasStudio.Web.Infrastructure.Assistant;

namespace RasStudio.Web.UnitTests.Assistant;

public sealed class RasStudioMcpClientTests
{
    [Fact]
    public void SafetyPolicyAllowsExplicitlyReadOnlyNonDestructiveTools()
    {
        var annotations = new ToolAnnotations { ReadOnlyHint = true, DestructiveHint = false };

        Assert.True(RasStudioMcpClient.IsSafeForAutomaticInvocation(annotations));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(true, null)]
    [InlineData(null, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void SafetyPolicyRejectsToolsWithoutSafeExplicitAnnotations(
        bool? readOnly,
        bool? destructive)
    {
        var annotations = new ToolAnnotations { ReadOnlyHint = readOnly, DestructiveHint = destructive };

        Assert.False(RasStudioMcpClient.IsSafeForAutomaticInvocation(annotations));
    }

    [Fact]
    public void SafetyPolicyRejectsMissingAnnotations()
    {
        Assert.False(RasStudioMcpClient.IsSafeForAutomaticInvocation(null));
    }
}
