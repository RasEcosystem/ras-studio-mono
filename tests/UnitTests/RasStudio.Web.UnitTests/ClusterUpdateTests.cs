using RasStudio.Application.Clusters;
using RasStudio.Web.Components;
using RasStudio.Web.Components.Pages;

namespace RasStudio.Web.UnitTests;

public sealed class ClusterUpdateTests
{
    [Fact]
    public void UnchangedEditorValuesProduceNoPatchSettings()
    {
        var cluster = CreateCluster();
        var values = CreateValues(cluster);

        var command = Clusters.CreateUpdateCommand(cluster, values);

        Assert.False(Clusters.HasSettingChanges(command));
        Assert.Null(command.Name);
        Assert.Null(command.MaxMemorySizeKb);
        Assert.Null(command.LoadBalancingMode);
    }

    [Fact]
    public void ChangedEditorValuesProduceOnlyChangedPatchSettings()
    {
        var cluster = CreateCluster();
        var values = CreateValues(cluster) with
        {
            MaxMemorySizeKb = 2048,
            AgentUser = "agent",
            AgentPassword = "secret"
        };

        var command = Clusters.CreateUpdateCommand(cluster, values);

        Assert.True(Clusters.HasSettingChanges(command));
        Assert.Equal(2048, command.MaxMemorySizeKb);
        Assert.Null(command.Name);
        Assert.Null(command.SecurityLevel);
        Assert.Equal("agent", command.AgentUser);
        Assert.Equal("secret", command.AgentPassword);
    }

    private static RasCluster CreateCluster()
    {
        return new RasCluster
        {
            Id = Guid.NewGuid(),
            Name = "Cluster One",
            Host = "cluster.example.test",
            Port = 1541,
            ExpirationTimeoutSeconds = 30,
            LifetimeLimitSeconds = 60,
            MaxMemorySizeKb = 1024,
            MaxMemoryTimeLimitSeconds = 90,
            SecurityLevel = 1,
            SessionFaultToleranceLevel = 2,
            LoadBalancingMode = RasClusterLoadBalancingMode.Performance,
            ErrorsCountThresholdPercent = 25,
            KillProblemProcesses = true,
            ObservedAt = DateTime.UtcNow
        };
    }

    private static RasClusterEditorValues CreateValues(RasCluster cluster)
    {
        return new RasClusterEditorValues(
            null,
            cluster.Name,
            null,
            null,
            cluster.ExpirationTimeoutSeconds,
            cluster.LifetimeLimitSeconds,
            cluster.MaxMemorySizeKb,
            cluster.MaxMemoryTimeLimitSeconds,
            cluster.SecurityLevel,
            cluster.SessionFaultToleranceLevel,
            cluster.LoadBalancingMode,
            cluster.ErrorsCountThresholdPercent,
            cluster.KillProblemProcesses,
            null,
            null);
    }
}
