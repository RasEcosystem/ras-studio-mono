using System.Net;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using RasStudio.Application.Clusters;
using RasStudio.Application.RasEndpoints;
using RasStudio.Application.RasHub;
using RasStudio.Web.Components.Features.Clusters;
using RasStudio.Web.Components.Shared.Tables;

namespace RasStudio.Web.Components.Pages;

public partial class Clusters
{
    private const int EndpointPageSize = 100;
    private const int RefreshGateConcurrency = 4;

    private static readonly DialogOptions EditorDialogOptions = new()
    {
        MaxWidth = MaxWidth.Medium,
        FullWidth = true,
        CloseButton = true,
        BackdropClick = false
    };

    private readonly CancellationTokenSource _disposeToken = new();
    private string? _appliedQuery;
    private Guid? _appliedRequestedEndpointId;
    private string? _appliedRequestedQuery;
    private Guid? _busyClusterId;
    private Guid? _busyEndpointId;
    private IReadOnlyList<RasEndpoint> _endpoints = [];
    private bool _initialized;
    private IReadOnlyList<ClusterRow> _items = [];
    private string? _loadError;
    private bool _loadPending;
    private int _loadedPageNumber = 1;
    private int _loadedPageSize = 10;
    private bool _loading;
    private bool _loadingEndpoints;
    private int _pageNumber = 1;
    private int _pageSize = 10;
    private string? _query;
    private bool _refreshing;
    private Guid? _selectedEndpointId;
    private int _totalCount;
    private int _totalPages;

    [Parameter]
    [SupplyParameterFromQuery(Name = "rasEndpointId")]
    public Guid? RequestedEndpointId { get; set; }

    [Parameter]
    [SupplyParameterFromQuery(Name = "q")]
    public string? RequestedQuery { get; set; }

    private bool IsBusy =>
        _loading || _loadingEndpoints || _refreshing || _busyEndpointId is not null || _busyClusterId is not null;

    private bool IsSearchDisabled =>
        _loadingEndpoints || _refreshing || _busyEndpointId is not null || _busyClusterId is not null;

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
            ApplyRequestedState();
            if (_loadError is null)
                await LoadPageAsync();
        }

        MarkRouteStateApplied();
        _initialized = true;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!_initialized || _loadingEndpoints || RouteStateIsApplied())
            return;

        ApplyRequestedState();
        MarkRouteStateApplied();
        _pageNumber = 1;
        ApplyPage([], 0, 0);
        await LoadPageAsync();
    }

    private void ApplyRequestedState()
    {
        _selectedEndpointId = _endpoints.Any(endpoint => endpoint.Id == RequestedEndpointId)
            ? RequestedEndpointId
            : null;
        _query = RequestedQuery;
        _appliedQuery = Normalize(RequestedQuery);
    }

    private async Task LoadEndpointsAsync()
    {
        try
        {
            _loadingEndpoints = true;
            _loadError = null;
            _endpoints = (await LoadAllEndpointsAsync())
                .Where(endpoint => endpoint.IsActive)
                .OrderBy(endpoint => endpoint.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(endpoint => endpoint.Id)
                .ToArray();

            if (_selectedEndpointId is { } selectedId &&
                _endpoints.All(endpoint => endpoint.Id != selectedId))
                _selectedEndpointId = null;
        }
        catch (OperationCanceledException) when (_disposeToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Unable to load RAS endpoints for clusters");
            _loadError = GetErrorMessage(exception, "Unable to load RAS endpoints from RasHub.");
        }
        finally
        {
            _loadingEndpoints = false;
        }
    }

    private async Task<IReadOnlyList<RasEndpoint>> LoadAllEndpointsAsync()
    {
        var endpoints = new List<RasEndpoint>();

        for (var pageNumber = 1; ; pageNumber++)
        {
            var page = await RasEndpointService.GetPageAsync(
                pageNumber,
                EndpointPageSize,
                _disposeToken.Token);
            endpoints.AddRange(page.Items);

            if (pageNumber >= page.TotalPages || page.Items.Count == 0)
                return endpoints.DistinctBy(endpoint => endpoint.Id).ToArray();
        }
    }

    private async Task ReloadEndpointsAsync()
    {
        await LoadEndpointsAsync();
        if (_loadError is null)
        {
            _pageNumber = 1;
            await LoadPageAsync();
        }
    }

    private async Task LoadPageAsync()
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
                else if (_selectedEndpointId is { } endpointId)
                    await LoadEndpointPageAsync(endpointId);
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
                Logger.LogError(exception, "Unable to load cluster shadow catalog");
                if (!_loadPending)
                {
                    _pageNumber = _loadedPageNumber;
                    _pageSize = _loadedPageSize;
                    _loadError = GetErrorMessage(exception, "Unable to load the cluster shadow from RasHub.");
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
        var page = await RasClusterService.SearchShadowPageAsync(
            query,
            selectedEndpointId,
            pageNumber,
            pageSize,
            _disposeToken.Token);

        if (page.TotalPages > 0 && pageNumber > page.TotalPages)
        {
            pageNumber = page.TotalPages;
            page = await RasClusterService.SearchShadowPageAsync(
                query,
                selectedEndpointId,
                pageNumber,
                pageSize,
                _disposeToken.Token);
        }

        if (_loadPending)
            return;

        _pageNumber = page.TotalCount == 0 ? 1 : pageNumber;
        ApplyPage(
            page.Items.Select(item => new ClusterRow(
                    item.RasEndpointId,
                    item.RasEndpointName,
                    item.Cluster))
                .ToArray(),
            page.TotalCount,
            page.TotalPages);
    }

    private async Task LoadEndpointPageAsync(Guid endpointId)
    {
        var pageNumber = _pageNumber;
        var pageSize = _pageSize;
        var page = await RasClusterService.GetShadowPageAsync(
            endpointId,
            pageNumber,
            pageSize,
            _disposeToken.Token);

        if (page.TotalPages > 0 && pageNumber > page.TotalPages)
        {
            pageNumber = page.TotalPages;
            page = await RasClusterService.GetShadowPageAsync(
                endpointId,
                pageNumber,
                pageSize,
                _disposeToken.Token);
        }

        var endpointName = SelectedEndpoint()?.Name ?? endpointId.ToString("D");
        if (_loadPending)
            return;

        _pageNumber = page.TotalCount == 0 ? 1 : pageNumber;
        ApplyPage(
            page.Items.Select(cluster => new ClusterRow(endpointId, endpointName, cluster))
                .ToArray(),
            page.TotalCount,
            page.TotalPages);
    }

    private async Task LoadCatalogPageAsync()
    {
        var pageNumber = _pageNumber;
        var pageSize = _pageSize;
        var sources = _endpoints;
        var page = await CatalogPager.LoadAsync(
            sources,
            pageNumber,
            pageSize,
            async (source, number, size, cancellationToken) =>
            {
                var sourcePage = await RasClusterService.GetShadowPageAsync(
                    source.Id,
                    number,
                    size,
                    cancellationToken);
                return new CatalogSlice<ClusterRow>(
                    sourcePage.Items.Select(item => new ClusterRow(
                            source.Id,
                            source.Name,
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

    private async Task RefreshShadowAsync()
    {
        if (IsBusy)
            return;

        try
        {
            _refreshing = true;
            await LoadEndpointsAsync();
            if (_loadError is not null || _disposeToken.IsCancellationRequested)
                return;
            if (_endpoints.Count == 0)
            {
                _pageNumber = 1;
                ApplyPage([], 0, 0);
                UpdateUrl();
                return;
            }

            var results = await RefreshAllEndpointsAsync();
            var succeeded = results.Where(result => result.Refresh is not null).ToArray();
            var failed = results.Where(result => result.Error is not null).ToArray();
            var clusterCount = succeeded.Sum(result => result.Refresh!.TotalCount);
            var gateCount = _endpoints.Select(endpoint => endpoint.RasGateId).Distinct().Count();

            if (failed.Length == 0)
                Snackbar.Add(
                    $"Refreshed {succeeded.Length:N0} RAS endpoint(s) across " +
                    $"{gateCount:N0} RasGate(s): {clusterCount:N0} cluster(s).",
                    Severity.Success);
            else if (succeeded.Length > 0)
                Snackbar.Add(
                    $"Refreshed {succeeded.Length:N0} of {_endpoints.Count:N0} RAS endpoint(s): " +
                    $"{clusterCount:N0} cluster(s). Failed: {FormatFailedEndpoints(failed)}.",
                    Severity.Warning);
            else
                Snackbar.Add(
                    $"Unable to refresh clusters from any RAS endpoint. " +
                    $"Failed: {FormatFailedEndpoints(failed)}.",
                    Severity.Error);

            _pageNumber = 1;
        }
        catch (OperationCanceledException) when (_disposeToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Unable to refresh cluster shadows from RAS endpoints");
            Snackbar.Add(
                GetErrorMessage(exception, "Unable to refresh cluster shadows from RAS."),
                Severity.Error);
        }
        finally
        {
            _refreshing = false;
        }

        await LoadPageAsync();
    }

    private async Task<IReadOnlyList<EndpointRefreshResult>> RefreshAllEndpointsAsync()
    {
        using var concurrency = new SemaphoreSlim(RefreshGateConcurrency);
        var tasks = _endpoints
            .GroupBy(endpoint => endpoint.RasGateId)
            .Select(async endpoints =>
            {
                await concurrency.WaitAsync(_disposeToken.Token);
                try
                {
                    var results = new List<EndpointRefreshResult>();
                    foreach (var endpoint in endpoints)
                        try
                        {
                            var refresh = await RasClusterService.RefreshShadowAsync(
                                endpoint.Id,
                                _disposeToken.Token);
                            results.Add(new EndpointRefreshResult(endpoint, refresh, null));
                        }
                        catch (OperationCanceledException) when (_disposeToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            Logger.LogError(
                                exception,
                                "Unable to refresh cluster shadow for RAS endpoint {RasEndpointId}",
                                endpoint.Id);
                            results.Add(new EndpointRefreshResult(
                                endpoint,
                                null,
                                GetErrorMessage(
                                    exception,
                                    "Unable to refresh through the assigned RasGate.")));
                        }

                    return results;
                }
                finally
                {
                    concurrency.Release();
                }
            });

        return (await Task.WhenAll(tasks)).SelectMany(results => results).ToArray();
    }

    private static string FormatFailedEndpoints(IReadOnlyList<EndpointRefreshResult> failed)
    {
        const int displayedEndpointCount = 3;
        var names = failed
            .Take(displayedEndpointCount)
            .Select(result => result.Endpoint.Name);
        var suffix = failed.Count > displayedEndpointCount
            ? $" and {failed.Count - displayedEndpointCount:N0} more"
            : string.Empty;
        return string.Join(", ", names) + suffix;
    }

    private async Task AddAsync()
    {
        var values = await ShowEditorAsync(null);
        if (values?.RasEndpointId is not { } endpointId ||
            values.Host is null ||
            values.Port is null)
            return;

        await ExecuteMutationAsync(
            endpointId,
            null,
            cancellationToken => RasClusterService.CreateAsync(
                endpointId,
                new CreateRasCluster(
                    values.Host,
                    values.Port.Value,
                    values.Name,
                    values.ExpirationTimeoutSeconds,
                    values.LifetimeLimitSeconds,
                    values.MaxMemorySizeKb,
                    values.MaxMemoryTimeLimitSeconds,
                    values.SecurityLevel,
                    values.SessionFaultToleranceLevel,
                    values.LoadBalancingMode,
                    values.ErrorsCountThresholdPercent,
                    values.KillProblemProcesses,
                    values.AgentUser,
                    values.AgentPassword),
                cancellationToken),
            "Cluster was created.",
            "Unable to create the cluster.",
            true);
    }

    private async Task EditAsync(ClusterRow row)
    {
        var cluster = row.Cluster;
        var values = await ShowEditorAsync(cluster);
        if (values is null)
            return;

        var command = CreateUpdateCommand(cluster, values);
        if (!HasSettingChanges(command))
        {
            Snackbar.Add("No cluster settings were changed.", Severity.Info);
            return;
        }

        await ExecuteMutationAsync(
            row.RasEndpointId,
            cluster.Id,
            cancellationToken => RasClusterService.UpdateAsync(
                row.RasEndpointId,
                cluster.Id,
                command,
                cancellationToken),
            $"{cluster.Name} was updated.",
            "Unable to update the cluster.");
    }

    private async Task RemoveAsync(ClusterRow row)
    {
        var cluster = row.Cluster;
        var parameters = new DialogParameters<ClusterCredentialsDialog>
        {
            {
                dialog => dialog.Description, $"Remove {cluster.Name} from {FormatClusterAddress(cluster)}? " +
                                              "This changes the live RAS cluster configuration."
            },
            { dialog => dialog.ActionText, "Remove cluster" },
            { dialog => dialog.ActionIcon, Icons.Material.Outlined.DeleteOutline },
            { dialog => dialog.ActionColor, Color.Error },
            { dialog => dialog.Severity, Severity.Warning }
        };
        var dialog = await DialogService.ShowAsync<ClusterCredentialsDialog>(
            "Remove cluster",
            parameters,
            EditorDialogOptions);
        var result = await dialog.Result;
        if (result is not { Canceled: false, Data: ClusterCredentialsValues credentials })
            return;

        await ExecuteMutationAsync(
            row.RasEndpointId,
            cluster.Id,
            cancellationToken => RasClusterService.RemoveAsync(
                row.RasEndpointId,
                cluster.Id,
                new RasClusterCredentials(credentials.User, credentials.Password),
                cancellationToken),
            $"{cluster.Name} was removed.",
            "Unable to remove the cluster.");
    }

    internal static UpdateRasCluster CreateUpdateCommand(
        RasCluster cluster,
        RasClusterEditorValues values)
    {
        return new UpdateRasCluster(
            Changed(values.Name, cluster.Name),
            Changed(values.ExpirationTimeoutSeconds, cluster.ExpirationTimeoutSeconds),
            Changed(values.LifetimeLimitSeconds, cluster.LifetimeLimitSeconds),
            Changed(values.MaxMemorySizeKb, cluster.MaxMemorySizeKb),
            Changed(values.MaxMemoryTimeLimitSeconds, cluster.MaxMemoryTimeLimitSeconds),
            Changed(values.SecurityLevel, cluster.SecurityLevel),
            Changed(values.SessionFaultToleranceLevel, cluster.SessionFaultToleranceLevel),
            Changed(values.LoadBalancingMode, cluster.LoadBalancingMode),
            Changed(values.ErrorsCountThresholdPercent, cluster.ErrorsCountThresholdPercent),
            Changed(values.KillProblemProcesses, cluster.KillProblemProcesses),
            values.AgentUser,
            values.AgentPassword);
    }

    internal static bool HasSettingChanges(UpdateRasCluster command)
    {
        return command.Name is not null ||
               command.ExpirationTimeoutSeconds is not null ||
               command.LifetimeLimitSeconds is not null ||
               command.MaxMemorySizeKb is not null ||
               command.MaxMemoryTimeLimitSeconds is not null ||
               command.SecurityLevel is not null ||
               command.SessionFaultToleranceLevel is not null ||
               command.LoadBalancingMode is not null ||
               command.ErrorsCountThresholdPercent is not null ||
               command.KillProblemProcesses is not null;
    }

    private static T? Changed<T>(T? value, T current)
        where T : struct
    {
        return value is { } candidate && !EqualityComparer<T>.Default.Equals(candidate, current)
            ? candidate
            : null;
    }

    private static string? Changed(string? value, string current)
    {
        return value is not null && !string.Equals(value, current, StringComparison.Ordinal)
            ? value
            : null;
    }

    private async Task<RasClusterEditorValues?> ShowEditorAsync(RasCluster? cluster)
    {
        var parameters = new DialogParameters<RasClusterEditorDialog>
        {
            { dialog => dialog.Endpoints, _endpoints },
            { dialog => dialog.InitialRasEndpointId, _selectedEndpointId }
        };
        if (cluster is not null)
            parameters.Add(dialog => dialog.Cluster, cluster);

        var dialog = await DialogService.ShowAsync<RasClusterEditorDialog>(
            cluster is null ? "Add cluster" : $"Edit {cluster.Name}",
            parameters,
            EditorDialogOptions);
        var result = await dialog.Result;

        return result is { Canceled: false, Data: RasClusterEditorValues values }
            ? values
            : null;
    }

    private async Task ShowDetailsAsync(RasCluster cluster)
    {
        var parameters = new DialogParameters<RasClusterDetailsDialog> { { dialog => dialog.Cluster, cluster } };
        await DialogService.ShowAsync<RasClusterDetailsDialog>(
            cluster.Name,
            parameters,
            EditorDialogOptions);
    }

    private async Task ExecuteMutationAsync(
        Guid endpointId,
        Guid? clusterId,
        Func<CancellationToken, Task<RasCluster>> operation,
        string successMessage,
        string fallbackError,
        bool resetToFirstPage = false)
    {
        if (_busyEndpointId is not null || _busyClusterId is not null)
            return;

        try
        {
            _busyEndpointId = endpointId;
            _busyClusterId = clusterId ?? Guid.Empty;
            await operation(_disposeToken.Token);
            Snackbar.Add(successMessage, Severity.Success);
            if (resetToFirstPage)
                _pageNumber = 1;
        }
        catch (OperationCanceledException) when (_disposeToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Cluster operation failed for {ClusterId}", clusterId);
            var message = exception is RasHubApiException
            {
                StatusCode: HttpStatusCode.Forbidden
            }
                ? "This RasHub API key is not allowed to manage clusters. Administrator access is required."
                : GetErrorMessage(exception, fallbackError);
            Snackbar.Add(message, Severity.Error);
        }
        finally
        {
            _busyEndpointId = null;
            _busyClusterId = null;
        }

        await LoadPageAsync();
    }

    private async Task EndpointChangedAsync(Guid? endpointId)
    {
        _selectedEndpointId = endpointId;
        _pageNumber = 1;
        ApplyPage([], 0, 0);
        await LoadPageAsync();
    }

    private async Task SearchChangedAsync(string? query)
    {
        _query = query;
        var normalized = Normalize(query);
        if (string.Equals(normalized, _appliedQuery, StringComparison.Ordinal))
            return;

        _appliedQuery = normalized;
        _pageNumber = 1;
        await LoadPageAsync();
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

    private async Task ReloadDataAsync()
    {
        if (IsBusy)
            return;

        await LoadEndpointsAsync();
        if (_loadError is null && !_disposeToken.IsCancellationRequested)
            await LoadPageAsync();
    }

    private async Task PageChangedAsync(int page)
    {
        _pageNumber = page;
        await LoadPageAsync();
    }

    private async Task PageSizeChangedAsync(int pageSize)
    {
        _pageSize = pageSize;
        _pageNumber = 1;
        await LoadPageAsync();
    }

    private void ApplyPage(IReadOnlyList<ClusterRow> items, int totalCount, int totalPages)
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
        if (_appliedQuery is not null)
            values.Add($"q={Uri.EscapeDataString(_appliedQuery)}");

        var target = values.Count == 0
            ? "clusters"
            : $"clusters?{string.Join('&', values)}";
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
        _appliedRequestedQuery = _appliedQuery;
    }

    private bool RouteStateIsApplied()
    {
        return RequestedEndpointId == _appliedRequestedEndpointId &&
               string.Equals(
                   Normalize(RequestedQuery),
                   _appliedRequestedQuery,
                   StringComparison.Ordinal);
    }

    private RasEndpoint? SelectedEndpoint()
    {
        return _endpoints.FirstOrDefault(endpoint => endpoint.Id == _selectedEndpointId);
    }

    private bool IsBusyCluster(ClusterRow row)
    {
        return _busyEndpointId == row.RasEndpointId && _busyClusterId == row.Cluster.Id;
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

    private static string InfobasesUrl(ClusterRow row)
    {
        return $"/infobases?rasEndpointId={row.RasEndpointId:D}&clusterId={row.Cluster.Id:D}";
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
            RasHubApiException { Code: "cluster_shadow_refresh_failed" } apiException =>
                "RasHub could not complete the live cluster refresh. Retry the operation; " +
                "if it fails again, verify the RAS endpoint address and the assigned " +
                $"RasGate API key.{FormatTrace(apiException)}",
            RasHubApiException apiException when apiException.TraceId is not null =>
                $"{apiException.Message} Trace: {apiException.TraceId}",
            RasHubApiException apiException => apiException.Message,
            RasHubConnectionNotConfiguredException notConfigured => notConfigured.Message,
            _ => fallback
        };
    }

    private static string FormatTrace(RasHubApiException exception)
    {
        return exception.TraceId is null ? string.Empty : $" Trace: {exception.TraceId}";
    }

    private sealed record ClusterRow(
        Guid RasEndpointId,
        string RasEndpointName,
        RasCluster Cluster);

    private sealed record EndpointRefreshResult(
        RasEndpoint Endpoint,
        RasClusterShadowRefresh? Refresh,
        string? Error);
}
