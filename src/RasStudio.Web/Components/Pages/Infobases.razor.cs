using Microsoft.AspNetCore.Components;
using MudBlazor;
using RasStudio.Application.Clusters;
using RasStudio.Application.Infobases;
using RasStudio.Application.RasEndpoints;
using RasStudio.Application.RasHub;
using RasStudio.Web.Components.Features.Clusters;
using RasStudio.Web.Components.Shared.Tables;

namespace RasStudio.Web.Components.Pages;

public partial class Infobases
{
    private const int CatalogRequestConcurrency = 8;

    private static readonly DialogOptions CredentialsDialogOptions = new()
    {
        MaxWidth = MaxWidth.Small,
        FullWidth = true,
        CloseButton = true,
        BackdropClick = false
    };

    private readonly CancellationTokenSource _disposeToken = new();
    private string? _appliedQuery;
    private Guid? _appliedRequestedClusterId;
    private Guid? _appliedRequestedEndpointId;
    private string? _appliedRequestedQuery;
    private IReadOnlyList<RasCluster> _clusters = [];
    private IReadOnlyList<RasEndpoint> _endpoints = [];
    private bool _initialized;
    private IReadOnlyList<InfobaseRow> _items = [];
    private string? _loadError;
    private bool _loadPending;
    private int _loadedPageNumber = 1;
    private int _loadedPageSize = 10;
    private bool _loading;
    private bool _loadingClusters;
    private bool _loadingEndpoints;
    private int _pageNumber = 1;
    private int _pageSize = 10;
    private string? _query;
    private Guid? _selectedClusterId;
    private Guid? _selectedEndpointId;
    private bool _syncingAll;
    private Guid? _syncingClusterId;
    private Guid? _syncingEndpointId;
    private Guid? _syncingInfobaseId;
    private int _totalCount;
    private int _totalPages;

    [Parameter]
    [SupplyParameterFromQuery(Name = "rasEndpointId")]
    public Guid? RequestedEndpointId { get; set; }

    [Parameter]
    [SupplyParameterFromQuery(Name = "clusterId")]
    public Guid? RequestedClusterId { get; set; }

    [Parameter]
    [SupplyParameterFromQuery(Name = "q")]
    public string? RequestedQuery { get; set; }

    private bool IsBusy =>
        _loading || _loadingEndpoints || _loadingClusters || _syncingAll ||
        _syncingInfobaseId is not null;

    private bool IsSearchDisabled =>
        _loadingEndpoints || _loadingClusters || _syncingAll ||
        _syncingInfobaseId is not null;

    public void Dispose()
    {
        _disposeToken.Cancel();
        _disposeToken.Dispose();
    }

    protected override async Task OnInitializedAsync()
    {
        if (ConnectionSettings.Current.IsConfigured)
        {
            await LoadEndpointsAsync();
            if (_loadError is null)
            {
                await ApplyRequestedStateAsync();
                if (_loadError is null)
                    await LoadAsync();
            }
        }

        MarkRouteStateApplied();
        _initialized = true;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!_initialized || _loadingEndpoints || RouteStateIsApplied())
            return;

        await ApplyRequestedStateAsync();
        if (_loadError is not null)
            return;

        MarkRouteStateApplied();
        _pageNumber = 1;
        ApplyPage([], 0, 0);
        await LoadAsync();
    }

    private async Task ApplyRequestedStateAsync()
    {
        _query = RequestedQuery;
        _appliedQuery = Normalize(RequestedQuery);
        _selectedEndpointId = _endpoints.Any(endpoint => endpoint.Id == RequestedEndpointId)
            ? RequestedEndpointId
            : null;
        await LoadClustersAsync();
        _selectedClusterId = _clusters.Any(cluster => cluster.Id == RequestedClusterId)
            ? RequestedClusterId
            : null;
    }

    private async Task LoadEndpointsAsync()
    {
        try
        {
            _loadingEndpoints = true;
            _loadError = null;
            _endpoints = (await RasEndpointService.GetAllAsync(_disposeToken.Token))
                .Where(endpoint => endpoint.IsActive)
                .OrderBy(endpoint => endpoint.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(endpoint => endpoint.Id)
                .ToArray();

            if (_selectedEndpointId is { } selectedId &&
                _endpoints.All(endpoint => endpoint.Id != selectedId))
            {
                _selectedEndpointId = null;
                _selectedClusterId = null;
            }
        }
        catch (OperationCanceledException) when (_disposeToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Unable to load RAS endpoints for infobases");
            _loadError = GetErrorMessage(exception, "Unable to load RAS endpoints from RasHub.");
        }
        finally
        {
            _loadingEndpoints = false;
        }
    }

    private async Task LoadClustersAsync()
    {
        _clusters = [];
        if (_selectedEndpointId is not { } endpointId)
            return;

        try
        {
            _loadingClusters = true;
            _loadError = null;
            _clusters = (await RasClusterService.GetShadowAllAsync(
                    endpointId,
                    _disposeToken.Token))
                .OrderBy(cluster => cluster.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(cluster => cluster.Id)
                .ToArray();
        }
        catch (OperationCanceledException) when (_disposeToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Unable to load clusters for infobase filters");
            _loadError = GetErrorMessage(exception, "Unable to load clusters from RasHub.");
        }
        finally
        {
            _loadingClusters = false;
        }
    }

    private async Task LoadAsync()
    {
        if (_loading)
        {
            _loadPending = true;
            return;
        }

        do
        {
            _loadPending = false;

            try
            {
                _loading = true;
                _loadError = null;

                if (_appliedQuery is { } query)
                    await LoadSearchPageAsync(query);
                else if (_selectedEndpointId is { } endpointId &&
                         _selectedClusterId is { } clusterId)
                    await LoadClusterPageAsync(endpointId, clusterId);
                else
                    await LoadCatalogPageAsync();

                if (!_loadPending)
                    UpdateUrl();
            }
            catch (OperationCanceledException) when (_disposeToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, "Unable to load infobase shadow catalog");
                if (!_loadPending)
                {
                    _pageNumber = _loadedPageNumber;
                    _pageSize = _loadedPageSize;
                    _loadError = GetErrorMessage(exception, "Unable to load infobases from RasHub.");
                }
            }
            finally
            {
                _loading = false;
            }
        } while (_loadPending && !_disposeToken.IsCancellationRequested);
    }

    private async Task LoadSearchPageAsync(string query)
    {
        var pageNumber = _pageNumber;
        var pageSize = _pageSize;
        var selectedEndpointId = _selectedEndpointId;
        var selectedClusterId = _selectedClusterId;
        var page = await RasInfobaseService.SearchShadowPageAsync(
            query,
            selectedEndpointId,
            selectedClusterId,
            pageNumber,
            pageSize,
            _disposeToken.Token);
        if (page.TotalPages > 0 && pageNumber > page.TotalPages)
        {
            pageNumber = page.TotalPages;
            page = await RasInfobaseService.SearchShadowPageAsync(
                query,
                selectedEndpointId,
                selectedClusterId,
                pageNumber,
                pageSize,
                _disposeToken.Token);
        }

        if (_loadPending)
            return;

        _pageNumber = page.TotalCount == 0 ? 1 : pageNumber;
        ApplyPage(
            page.Items.Select(item => new InfobaseRow(
                    item.RasEndpointId,
                    item.RasEndpointName,
                    item.ClusterId,
                    item.ClusterName,
                    item.Infobase))
                .ToArray(),
            page.TotalCount,
            page.TotalPages);
    }

    private async Task LoadClusterPageAsync(Guid endpointId, Guid clusterId)
    {
        var pageNumber = _pageNumber;
        var pageSize = _pageSize;
        var page = await RasInfobaseService.GetShadowPageAsync(
            endpointId,
            clusterId,
            pageNumber,
            pageSize,
            _disposeToken.Token);
        if (page.TotalPages > 0 && pageNumber > page.TotalPages)
        {
            pageNumber = page.TotalPages;
            page = await RasInfobaseService.GetShadowPageAsync(
                endpointId,
                clusterId,
                pageNumber,
                pageSize,
                _disposeToken.Token);
        }

        var endpointName = SelectedEndpoint()?.Name ?? endpointId.ToString("D");
        var clusterName = SelectedCluster()?.Name ?? clusterId.ToString("D");
        if (_loadPending)
            return;

        _pageNumber = page.TotalCount == 0 ? 1 : pageNumber;
        ApplyPage(
            page.Items.Select(infobase => new InfobaseRow(
                    endpointId,
                    endpointName,
                    clusterId,
                    clusterName,
                    infobase))
                .ToArray(),
            page.TotalCount,
            page.TotalPages);
    }

    private async Task LoadCatalogPageAsync()
    {
        var pageNumber = _pageNumber;
        var pageSize = _pageSize;
        var sources = await LoadClusterScopesAsync();
        var page = await CatalogPager.LoadAsync(
            sources,
            pageNumber,
            pageSize,
            async (source, number, size, cancellationToken) =>
            {
                var sourcePage = await RasInfobaseService.GetShadowPageAsync(
                    source.RasEndpointId,
                    source.Cluster.Id,
                    number,
                    size,
                    cancellationToken);
                return new CatalogSlice<InfobaseRow>(
                    sourcePage.Items.Select(item => new InfobaseRow(
                            source.RasEndpointId,
                            source.RasEndpointName,
                            source.Cluster.Id,
                            source.Cluster.Name,
                            item))
                        .ToArray(),
                    sourcePage.TotalCount);
            },
            _disposeToken.Token);
        if (_loadPending)
            return;

        _pageNumber = page.Page;
        ApplyPage(page.Items, page.TotalCount, page.TotalPages);
    }

    private async Task<IReadOnlyList<ClusterScope>> LoadClusterScopesAsync()
    {
        if (_selectedEndpointId is { } endpointId)
        {
            var endpointName = SelectedEndpoint()?.Name ?? endpointId.ToString("D");
            return _clusters.Select(cluster => new ClusterScope(
                    endpointId,
                    endpointName,
                    cluster))
                .ToArray();
        }

        using var concurrency = new SemaphoreSlim(CatalogRequestConcurrency);
        var tasks = _endpoints.Select(async endpoint =>
        {
            await concurrency.WaitAsync(_disposeToken.Token);
            try
            {
                var clusters = await RasClusterService.GetShadowAllAsync(
                    endpoint.Id,
                    _disposeToken.Token);
                return clusters.OrderBy(cluster => cluster.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(cluster => cluster.Id)
                    .Select(cluster => new ClusterScope(
                        endpoint.Id,
                        endpoint.Name,
                        cluster))
                    .ToArray();
            }
            finally
            {
                concurrency.Release();
            }
        });

        var groups = await Task.WhenAll(tasks);
        return groups.SelectMany(group => group).ToArray();
    }

    private async Task EndpointChangedAsync(Guid? endpointId)
    {
        _selectedEndpointId = endpointId;
        _selectedClusterId = null;
        _pageNumber = 1;
        await LoadClustersAsync();
        if (_loadError is not null)
            return;

        await LoadAsync();
    }

    private async Task ClusterChangedAsync(Guid? clusterId)
    {
        _selectedClusterId = clusterId;
        _pageNumber = 1;
        await LoadAsync();
    }

    private async Task SearchChangedAsync(string? query)
    {
        _query = query;
        var normalized = Normalize(query);
        if (string.Equals(normalized, _appliedQuery, StringComparison.Ordinal))
            return;

        _appliedQuery = normalized;
        _pageNumber = 1;
        await LoadAsync();
    }

    private Task ClearSearchAsync()
    {
        return SearchChangedAsync(null);
    }

    private static string ProgressClass(bool isVisible)
    {
        return isVisible
            ? "app-table-progress app-table-progress--visible"
            : "app-table-progress";
    }

    private Task RefreshShadowAsync()
    {
        return SynchronizeShadowAsync(null);
    }

    private async Task RefreshShadowWithCredentialsAsync()
    {
        if (_selectedEndpointId is null || _selectedClusterId is null)
            return;

        var credentials = await RequestCredentialsAsync(
            "Synchronize infobases",
            $"Fetch the complete live infobase snapshot for {SelectedCluster()?.Name ?? "the selected cluster"}.",
            "Synchronize",
            Icons.Material.Outlined.Sync,
            Color.Primary,
            Severity.Info);
        if (credentials is null)
            return;

        await SynchronizeShadowAsync(
            new RasInfobaseCredentials(credentials.User, credentials.Password));
    }

    private async Task SynchronizeShadowAsync(RasInfobaseCredentials? credentials)
    {
        if (_selectedEndpointId is not { } endpointId ||
            _selectedClusterId is not { } clusterId)
            return;

        try
        {
            _syncingAll = true;
            var refresh = await RasInfobaseService.RefreshShadowAsync(
                endpointId,
                clusterId,
                credentials,
                _disposeToken.Token);
            Snackbar.Add(
                $"Infobase shadow synchronized: {refresh.TotalCount:N0} infobase(s).",
                Severity.Success);
            _pageNumber = 1;
        }
        catch (OperationCanceledException) when (_disposeToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Unable to synchronize infobases for {ClusterId}", clusterId);
            Snackbar.Add(
                GetErrorMessage(exception, "Unable to synchronize infobases through RasGate."),
                Severity.Error);
        }
        finally
        {
            _syncingAll = false;
        }

        await LoadAsync();
    }

    private Task RefreshOneAsync(InfobaseRow row)
    {
        return SynchronizeOneAsync(row, null);
    }

    private async Task RefreshOneWithCredentialsAsync(InfobaseRow row)
    {
        var credentials = await RequestCredentialsAsync(
            "Synchronize infobase",
            $"Fetch the current live state of {row.Infobase.Name}.",
            "Synchronize",
            Icons.Material.Outlined.Sync,
            Color.Primary,
            Severity.Info);
        if (credentials is null)
            return;

        await SynchronizeOneAsync(
            row,
            new RasInfobaseCredentials(credentials.User, credentials.Password));
    }

    private async Task SynchronizeOneAsync(
        InfobaseRow row,
        RasInfobaseCredentials? credentials)
    {
        try
        {
            _syncingEndpointId = row.RasEndpointId;
            _syncingClusterId = row.ClusterId;
            _syncingInfobaseId = row.Infobase.Id;
            await RasInfobaseService.RefreshAsync(
                row.RasEndpointId,
                row.ClusterId,
                row.Infobase.Id,
                credentials,
                _disposeToken.Token);
            Snackbar.Add($"{row.Infobase.Name} was synchronized.", Severity.Success);
        }
        catch (OperationCanceledException) when (_disposeToken.IsCancellationRequested)
        {
            return;
        }
        catch (RasHubApiException exception)
            when (exception.Code == "infobase_not_found")
        {
            Snackbar.Add(
                $"{row.Infobase.Name} is no longer present in RAS and was removed from the shadow.",
                Severity.Warning);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Unable to synchronize infobase {InfobaseId}", row.Infobase.Id);
            Snackbar.Add(
                GetErrorMessage(exception, "Unable to synchronize the infobase through RasGate."),
                Severity.Error);
        }
        finally
        {
            _syncingEndpointId = null;
            _syncingClusterId = null;
            _syncingInfobaseId = null;
        }

        await LoadAsync();
    }

    private async Task<ClusterCredentialsValues?> RequestCredentialsAsync(
        string title,
        string description,
        string actionText,
        string actionIcon,
        Color actionColor,
        Severity severity)
    {
        var parameters = new DialogParameters<ClusterCredentialsDialog>
        {
            { dialog => dialog.Description, description },
            { dialog => dialog.ActionText, actionText },
            { dialog => dialog.ActionIcon, actionIcon },
            { dialog => dialog.ActionColor, actionColor },
            { dialog => dialog.Severity, severity }
        };
        var dialog = await DialogService.ShowAsync<ClusterCredentialsDialog>(
            title,
            parameters,
            CredentialsDialogOptions);
        var result = await dialog.Result;

        return result is { Canceled: false, Data: ClusterCredentialsValues values }
            ? values
            : null;
    }

    private async Task PageChangedAsync(int page)
    {
        _pageNumber = page;
        await LoadAsync();
    }

    private async Task PageSizeChangedAsync(int pageSize)
    {
        _pageSize = pageSize;
        _pageNumber = 1;
        await LoadAsync();
    }

    private async Task ReloadDataAsync()
    {
        if (!IsBusy)
            await RetryLoadAsync();
    }

    private async Task RetryLoadAsync()
    {
        await LoadEndpointsAsync();
        if (_loadError is not null)
            return;

        await LoadClustersAsync();
        if (_loadError is not null)
            return;

        if (_selectedClusterId is { } clusterId &&
            _clusters.All(cluster => cluster.Id != clusterId))
            _selectedClusterId = null;

        if (!_disposeToken.IsCancellationRequested)
            await LoadAsync();
    }

    private void ApplyPage(IReadOnlyList<InfobaseRow> items, int totalCount, int totalPages)
    {
        _loadedPageNumber = _pageNumber;
        _loadedPageSize = _pageSize;
        _items = items;
        _totalCount = totalCount;
        _totalPages = totalPages;
    }

    private void UpdateUrl()
    {
        var values = new List<string>();
        if (_selectedEndpointId is { } endpointId)
            values.Add($"rasEndpointId={endpointId:D}");
        if (_selectedClusterId is { } clusterId)
            values.Add($"clusterId={clusterId:D}");
        if (_appliedQuery is not null)
            values.Add($"q={Uri.EscapeDataString(_appliedQuery)}");

        var target = values.Count == 0
            ? "infobases"
            : $"infobases?{string.Join('&', values)}";
        MarkRouteStateApplied();
        if (string.Equals(
                Navigation.ToBaseRelativePath(Navigation.Uri),
                target,
                StringComparison.Ordinal))
            return;

        Navigation.NavigateTo($"/{target}", replace: true);
    }

    private void MarkRouteStateApplied()
    {
        _appliedRequestedEndpointId = _selectedEndpointId;
        _appliedRequestedClusterId = _selectedClusterId;
        _appliedRequestedQuery = _appliedQuery;
    }

    private bool RouteStateIsApplied()
    {
        return RequestedEndpointId == _appliedRequestedEndpointId &&
               RequestedClusterId == _appliedRequestedClusterId &&
               string.Equals(
                   Normalize(RequestedQuery),
                   _appliedRequestedQuery,
                   StringComparison.Ordinal);
    }

    private bool IsSyncing(InfobaseRow row)
    {
        return _syncingEndpointId == row.RasEndpointId &&
               _syncingClusterId == row.ClusterId &&
               _syncingInfobaseId == row.Infobase.Id;
    }

    private RasEndpoint? SelectedEndpoint()
    {
        return _endpoints.FirstOrDefault(item => item.Id == _selectedEndpointId);
    }

    private RasCluster? SelectedCluster()
    {
        return _clusters.FirstOrDefault(item => item.Id == _selectedClusterId);
    }

    private static string FormatEndpointAddress(RasEndpoint endpoint)
    {
        return endpoint.Host.Contains(':', StringComparison.Ordinal)
            ? $"[{endpoint.Host}]:{endpoint.Port}"
            : $"{endpoint.Host}:{endpoint.Port}";
    }

    private static string FormatClusterAddress(RasCluster cluster)
    {
        return cluster.Host.Contains(':', StringComparison.Ordinal)
            ? $"[{cluster.Host}]:{cluster.Port}"
            : $"{cluster.Host}:{cluster.Port}";
    }

    private static string FormatTime(DateTime value)
    {
        return value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string GetErrorMessage(Exception exception, string fallback)
    {
        return exception switch
        {
            RasHubApiException apiException when apiException.TraceId is not null =>
                $"{apiException.Message} Trace: {apiException.TraceId}",
            RasHubApiException apiException => apiException.Message,
            RasHubConnectionNotConfiguredException notConfigured => notConfigured.Message,
            _ => fallback
        };
    }

    private sealed record ClusterScope(
        Guid RasEndpointId,
        string RasEndpointName,
        RasCluster Cluster);

    private sealed record InfobaseRow(
        Guid RasEndpointId,
        string RasEndpointName,
        Guid ClusterId,
        string ClusterName,
        RasInfobase Infobase);
}
