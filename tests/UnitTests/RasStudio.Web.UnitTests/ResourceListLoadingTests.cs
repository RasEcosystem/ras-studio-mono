using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using RasStudio.Application.Clusters;
using RasStudio.Application.Infobases;
using RasStudio.Application.RasEndpoints;
using RasStudio.Application.RasGates;
using RasStudio.Application.RasHub;
using RasStudio.Web.Components.Pages;

namespace RasStudio.Web.UnitTests;

public sealed class ResourceListLoadingTests
{
    [Theory]
    [InlineData(typeof(Clusters), false)]
    [InlineData(typeof(Clusters), true)]
    [InlineData(typeof(Infobases), false)]
    [InlineData(typeof(Infobases), true)]
    [InlineData(typeof(RasGates), false)]
    [InlineData(typeof(RasGates), true)]
    [InlineData(typeof(RasEndpoints), false)]
    [InlineData(typeof(RasEndpoints), true)]
    public async Task FailedNavigationPreservesLoadedRowsAndPagination(Type componentType, bool changeSize)
    {
        await using var harness = new ListHarness(componentType);
        await harness.CallAsync("PageChangedAsync", 2);
        Assert.Null(harness.Get("_loadError"));
        var previousRows = harness.Rows;
        harness.Fail = true;

        await harness.CallAsync(changeSize ? "PageSizeChangedAsync" : "PageChangedAsync", changeSize ? 25 : 1);

        Assert.NotNull(harness.Get("_loadError"));
        Assert.Same(previousRows, harness.Rows);
        Assert.Equal(2, harness.Get("_pageNumber"));
        Assert.Equal(10, harness.Get("_pageSize"));
        Assert.Equal((2, 10), harness.DisplayedPagination);

        harness.Fail = false;
        await harness.CallAsync("PageChangedAsync", 1);
        Assert.Null(harness.Get("_loadError"));
        Assert.Equal((1, 10), harness.DisplayedPagination);
    }

    [Theory]
    [InlineData(typeof(Clusters))]
    [InlineData(typeof(Infobases))]
    public async Task GlobalCatalogPreservesHubOrderingAcrossPages(Type componentType)
    {
        await using var harness = new ListHarness(componentType);
        var actual = new List<string>();
        for (var page = 1; page <= 2; page++)
        {
            await harness.CallAsync("PageChangedAsync", page);
            Assert.Null(harness.Get("_loadError"));
            actual.AddRange(((IEnumerable)harness.Rows).Cast<object>().Select(row =>
                (string)Property(Property(row, componentType == typeof(Clusters) ? "Cluster" : "Infobase"), "Name")));
        }

        Assert.Equal(ListHarness.Names, actual);
    }

    [Theory]
    [InlineData(typeof(Clusters))]
    [InlineData(typeof(Infobases))]
    public async Task ReloadDiscoversEndpointsAndClearsRemovedEndpointFilter(Type componentType)
    {
        await using var harness = new ListHarness(componentType);
        var original = harness.Endpoints[0];
        harness.Set("_selectedEndpointId", original.Id);
        var added = original with { Id = Guid.NewGuid(), Name = "New endpoint" };
        harness.Endpoints = [original with { IsActive = false }, added];

        await harness.CallAsync("ReloadDataAsync");

        Assert.Null(harness.Get("_loadError"));
        Assert.Null(harness.Get("_selectedEndpointId"));
        Assert.Equal([added.Id],
            ((IEnumerable<RasEndpoint>)harness.Get("_endpoints")!).Select(endpoint => endpoint.Id));
        Assert.Contains(added.Id, harness.RequestedEndpoints);
        Assert.DoesNotContain(original.Id, harness.RequestedEndpoints);
    }

    [Theory]
    [InlineData(typeof(Clusters))]
    [InlineData(typeof(Infobases))]
    public async Task FailedMetadataReloadKeepsRowsAndReportsFailure(Type componentType)
    {
        await using var harness = new ListHarness(componentType);
        await harness.CallAsync("PageChangedAsync", 2);
        var previousRows = harness.Rows;
        harness.Fail = true;

        await harness.CallAsync("ReloadDataAsync");

        Assert.NotNull(harness.Get("_loadError"));
        Assert.Same(previousRows, harness.Rows);
        Assert.Equal((2, 10), harness.DisplayedPagination);
    }

    [Fact]
    public async Task InfobaseReloadRefreshesClustersAndClearsRemovedClusterFilter()
    {
        await using var harness = new ListHarness(typeof(Infobases));
        var previous = harness.Clusters[0];
        harness.Set("_selectedEndpointId", harness.Endpoints[0].Id);
        harness.Set("_selectedClusterId", previous.Id);
        harness.Clusters = [previous with { Id = Guid.NewGuid(), Name = "New cluster" }];

        await harness.CallAsync("ReloadDataAsync");

        Assert.Null(harness.Get("_loadError"));
        Assert.Null(harness.Get("_selectedClusterId"));
        Assert.Equal(harness.Endpoints[0].Id, harness.Get("_selectedEndpointId"));
        Assert.Contains(harness.Clusters[0].Id, harness.RequestedClusters);
        Assert.DoesNotContain(previous.Id, harness.RequestedClusters);
    }

    [Fact]
    public async Task RefreshAllDiscoversEndpointsEvenWhenCachedCatalogIsEmpty()
    {
        await using var harness = new ListHarness(typeof(Clusters));
        harness.Set("_endpoints", Array.Empty<RasEndpoint>());
        harness.Endpoints =
        [
            harness.Endpoints[0],
            harness.Endpoints[0] with { Id = Guid.NewGuid(), RasGateId = Guid.NewGuid() },
            harness.Endpoints[0] with { Id = Guid.NewGuid(), IsActive = false }
        ];

        await harness.CallAsync("RefreshShadowAsync");

        Assert.Null(harness.Get("_loadError"));
        Assert.Equal(harness.Endpoints.Where(endpoint => endpoint.IsActive).Select(endpoint => endpoint.Id).Order(),
            harness.RefreshedEndpoints.Order());
    }

    [Theory]
    [InlineData(typeof(Clusters), false)]
    [InlineData(typeof(Clusters), true)]
    [InlineData(typeof(Infobases), false)]
    [InlineData(typeof(Infobases), true)]
    [InlineData(typeof(RasGates), false)]
    [InlineData(typeof(RasGates), true)]
    public async Task ObsoleteSearchDoesNotOverwriteQueuedSearch(Type componentType, bool failObsoleteRequest)
    {
        await using var harness = new ListHarness(componentType);
        await harness.CallAsync("PageChangedAsync", 2);
        var previousRows = harness.Rows;
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.BeforeSearch = query => query == "old" ? pending.Task : Task.CompletedTask;

        var first = harness.CallAsync("SearchChangedAsync", "old");
        await harness.CallAsync("SearchChangedAsync", "latest");
        Assert.Same(previousRows, harness.Rows);
        Assert.Equal((2, 10), harness.DisplayedPagination);
        if (failObsoleteRequest)
            pending.SetException(new IOException("Obsolete search failed"));
        else
            pending.SetResult();
        await first;

        Assert.Null(harness.Get("_loadError"));
        Assert.Equal(["old", "latest"], harness.Searches);
        Assert.Equal((1, 10), harness.DisplayedPagination);
        Assert.Equal("latest", harness.Get(componentType == typeof(RasGates) ? "_appliedSearch" : "_appliedQuery"));
    }

    private static object Property(object instance, string name)
    {
        return instance.GetType().GetProperty(name)!.GetValue(instance)!;
    }

    private sealed class ListHarness : IAsyncDisposable
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        internal static readonly string[] Names = ["B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "a"];
        private readonly object _component;

        internal ListHarness(Type componentType)
        {
            _component = Activator.CreateInstance(componentType)!;
            Set("Logger", Activator.CreateInstance(typeof(NullLogger<>).MakeGenericType(componentType)));
            Set("ConnectionSettings",
                Proxy<IRasHubConnectionSettings>((_, _) => new RasHubConnectionState("http://localhost", true)));
            if (componentType != typeof(RasGates))
                Set("RasEndpointService", Proxy<IRasEndpointService>(EndpointCall));
            if (componentType == typeof(Clusters) || componentType == typeof(Infobases))
            {
                Set("Navigation", new TestNavigation());
                Set("_endpoints", Endpoints);
                Set("RasClusterService", Proxy<IRasClusterService>(ClusterCall));
                if (componentType == typeof(Infobases))
                {
                    Set("_clusters", Clusters);
                    Set("RasInfobaseService", Proxy<IRasInfobaseService>(InfobaseCall));
                }
                else
                {
                    Set("Snackbar", Proxy<ISnackbar>((_, _) => null));
                }
            }
            else
            {
                Set("RasGateService", Proxy<IRasGateService>(GateCall));
            }
        }

        internal RasEndpoint[] Endpoints { get; set; } =
        [
            new(Guid.NewGuid(),
                Guid.NewGuid(),
                "Endpoint",
                "localhost",
                1545,
                true,
                null,
                1,
                DateTime.UtcNow,
                DateTime.UtcNow)
        ];

        internal RasCluster[] Clusters { get; set; } = Names.Select(name => new RasCluster
        {
            Id = Guid.NewGuid(),
            Name = name,
            Host = "localhost",
            Port = 1541,
            ExpirationTimeoutSeconds = 0,
            LifetimeLimitSeconds = 0,
            MaxMemorySizeKb = 0,
            MaxMemoryTimeLimitSeconds = 0,
            SecurityLevel = 0,
            SessionFaultToleranceLevel = 0,
            LoadBalancingMode = RasClusterLoadBalancingMode.Performance,
            ErrorsCountThresholdPercent = 0,
            KillProblemProcesses = false,
            ObservedAt = DateTime.UtcNow
        }).ToArray();

        internal bool Fail { get; set; }
        internal List<Guid> RequestedEndpoints { get; } = [];
        internal List<Guid> RequestedClusters { get; } = [];
        internal List<Guid> RefreshedEndpoints { get; } = [];
        internal List<string> Searches { get; } = [];
        internal Func<string, Task> BeforeSearch { get; set; } = _ => Task.CompletedTask;

        internal object Rows => Get("_items") ?? Property(Get("_page")!, "Items");

        internal (int, int) DisplayedPagination => Get("_page") is { } page
            ? ((int)Property(page, "Page"), (int)Property(page, "PageSize"))
            : ((int)Get("_loadedPageNumber")!, (int)Get("_loadedPageSize")!);

        public async ValueTask DisposeAsync()
        {
            if (_component is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else
                ((IDisposable)_component).Dispose();
        }

        internal object? Get(string name)
        {
            return _component.GetType().GetField(name, Flags)?.GetValue(_component);
        }

        internal void Set(string name, object? value)
        {
            if (_component.GetType().GetField(name, Flags) is { } field)
                field.SetValue(_component, value);
            else
                _component.GetType().GetProperty(name, Flags)!.SetValue(_component, value);
        }

        internal Task CallAsync(string name, params object?[] args)
        {
            return (Task)_component.GetType().GetMethod(name, Flags)!.Invoke(_component, args)!;
        }

        private object EndpointCall(MethodInfo method, object?[] args)
        {
            if (Fail)
                throw new IOException("Endpoint unavailable");
            if (method.Name == "GetAllAsync")
                return Task.FromResult<IReadOnlyList<RasEndpoint>>(Endpoints);
            var number = (int)args[0]!;
            var size = (int)args[1]!;
            var data = _component is RasEndpoints
                ? Enumerable.Range(0, 11).Select(_ => Endpoints[0] with { Id = Guid.NewGuid() }).ToArray()
                : Endpoints;
            return Task.FromResult(new RasEndpointPage(Slice(data, number, size),
                data.Length,
                number,
                size,
                Pages(data.Length, size)));
        }

        private object ClusterCall(MethodInfo method, object?[] args)
        {
            if (Fail)
                throw new IOException("Cluster unavailable");
            if (method.Name == "SearchShadowPageAsync")
                return SearchAsync((string)args[0]!,
                    new RasClusterSearchPage(
                        [new RasClusterSearchResult(Endpoints[0].Id, Endpoints[0].Name, Clusters[0])],
                        1,
                        (int)args[2]!,
                        (int)args[3]!,
                        1));
            var endpointId = (Guid)args[0]!;
            RequestedEndpoints.Add(endpointId);
            if (method.Name == "GetShadowAllAsync")
                return Task.FromResult<IReadOnlyList<RasCluster>>([Clusters[0]]);
            if (method.Name == "RefreshShadowAsync")
            {
                RefreshedEndpoints.Add(endpointId);
                return Task.FromResult(new RasClusterShadowRefresh(Clusters.Length, DateTime.UtcNow));
            }

            var number = (int)args[1]!;
            var size = (int)args[2]!;
            return Task.FromResult(new RasClusterPage(Slice(Clusters, number, size),
                Clusters.Length,
                number,
                size,
                Pages(Clusters.Length, size)));
        }

        private object InfobaseCall(MethodInfo method, object?[] args)
        {
            if (Fail)
                throw new IOException("Infobase unavailable");
            var data = Names.Select(name => new RasInfobase(Guid.NewGuid(), name, "", DateTime.UtcNow)).ToArray();
            if (method.Name == "SearchShadowPageAsync")
                return SearchAsync((string)args[0]!,
                    new RasInfobaseSearchPage(
                        [
                            new RasInfobaseSearchResult(Endpoints[0].Id,
                                Endpoints[0].Name,
                                Clusters[0].Id,
                                Clusters[0].Name,
                                data[0])
                        ],
                        1,
                        (int)args[3]!,
                        (int)args[4]!,
                        1));
            RequestedEndpoints.Add((Guid)args[0]!);
            RequestedClusters.Add((Guid)args[1]!);
            var number = (int)args[2]!;
            var size = (int)args[3]!;
            return Task.FromResult(new RasInfobasePage(Slice(data, number, size),
                data.Length,
                number,
                size,
                Pages(data.Length, size)));
        }

        private object GateCall(MethodInfo method, object?[] args)
        {
            if (Fail)
                throw new IOException("Gate unavailable");
            if (method.Name != "GetPageAsync")
                throw new NotSupportedException(method.Name);
            var number = (int)args[0]!;
            var size = (int)args[1]!;
            var data = Names.Select(name => new RasGate(Guid.NewGuid(),
                name,
                "http://localhost",
                80,
                false,
                1,
                DateTime.UtcNow,
                DateTime.UtcNow)).ToArray();
            var page = new RasGatePage(Slice(data, number, size), data.Length, number, size, Pages(data.Length, size));
            return args[2] is string query ? SearchAsync(query, page) : Task.FromResult(page);
        }

        private async Task<T> SearchAsync<T>(string query, T result)
        {
            Searches.Add(query);
            await BeforeSearch(query);
            return result;
        }

        private static T[] Slice<T>(T[] items, int number, int size)
        {
            return items.Skip((number - 1) * size).Take(size).ToArray();
        }

        private static int Pages(int count, int size)
        {
            return (count - 1) / size + 1;
        }

        private static T Proxy<T>(Func<MethodInfo, object?[], object?> handler) where T : class
        {
            var proxy = DispatchProxy.Create<T, ServiceProxy>();
            ((ServiceProxy)(object)proxy).Handler = handler;
            return proxy;
        }
    }

    public class ServiceProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Handler { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return Handler(targetMethod!, args!);
        }
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation()
        {
            Initialize("http://localhost/", "http://localhost/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Uri = ToAbsoluteUri(uri).ToString();
        }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }
}
