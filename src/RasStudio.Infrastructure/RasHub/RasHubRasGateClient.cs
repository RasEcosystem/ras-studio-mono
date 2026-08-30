using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RasHub.Contracts.Common;
using RasHub.Contracts.Common.Pagination;
using RasHub.Contracts.RasHub.Requests;
using RasHub.Contracts.RasHub.Responses;
using RasStudio.Application.RasGates;
using RasStudio.Application.RasHub;
using ContractHealth = RasHub.Contracts.RasHub.Models.RasGateHealthState;
using ContractRasGate = RasHub.Contracts.RasHub.Models.RasGateModel;

namespace RasStudio.Infrastructure.RasHub;

public sealed class RasHubRasGateClient(
    HttpClient httpClient,
    IRasHubConnectionProvider connectionProvider,
    ILogger<RasHubRasGateClient> logger) : IRasGateService
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string TraceIdHeaderName = "X-Trace-Id";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public Task<RasGatePage> GetPageAsync(
        int page,
        int pageSize,
        string? query = null,
        IReadOnlyCollection<RasGateSearchField>? fields = null,
        CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);

        var search = NormalizeQuery(query);
        var path = search is null
            ? $"api/v1/ras-gates?page={page}&pageSize={pageSize}"
            : $"api/v1/ras-gates/search?{BuildSearchQuery(search, fields)}&page={page}&pageSize={pageSize}";

        return SendAndMapAsync<PageResult<ContractRasGate>, RasGatePage>(
            HttpMethod.Get,
            path,
            null,
            MapPage,
            cancellationToken);
    }

    public Task<IReadOnlyList<RasGate>> GetAllAsync(
        string? query = null,
        IReadOnlyCollection<RasGateSearchField>? fields = null,
        CancellationToken cancellationToken = default)
    {
        var search = NormalizeQuery(query);
        var path = search is null
            ? "api/v1/ras-gates/all"
            : $"api/v1/ras-gates/search/all?{BuildSearchQuery(search, fields)}";

        return SendAndMapAsync<IReadOnlyList<ContractRasGate>, IReadOnlyList<RasGate>>(
            HttpMethod.Get,
            path,
            null,
            models => models.Select(MapGate).ToArray(),
            cancellationToken);
    }

    public Task<RasGate> GetAsync(
        Guid rasGateId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasGateId);
        return SendAndMapAsync<ContractRasGate, RasGate>(
            HttpMethod.Get,
            $"api/v1/ras-gates/{rasGateId:D}",
            null,
            MapGate,
            cancellationToken);
    }

    public Task<RasGate> CreateAsync(
        CreateRasGate command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = new CreateRasGateRequest(
            command.Name,
            command.Url,
            command.Port,
            command.ApiKey,
            command.IsActive);

        return SendAndMapAsync<ContractRasGate, RasGate>(
            HttpMethod.Post,
            "api/v1/ras-gates",
            request,
            MapGate,
            cancellationToken);
    }

    public Task<RasGate> UpdateAsync(
        Guid rasGateId,
        UpdateRasGate command,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasGateId);
        ArgumentNullException.ThrowIfNull(command);

        var request = new UpdateRasGateRequest(
            command.Name,
            command.Url,
            command.Port,
            command.IsActive,
            command.ApiKey);

        return SendAndMapAsync<ContractRasGate, RasGate>(
            HttpMethod.Put,
            $"api/v1/ras-gates/{rasGateId:D}",
            request,
            MapGate,
            cancellationToken);
    }

    public Task<RasGate> DeleteAsync(
        Guid rasGateId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasGateId);
        return SendAndMapAsync<ContractRasGate, RasGate>(
            HttpMethod.Delete,
            $"api/v1/ras-gates/{rasGateId:D}",
            null,
            MapGate,
            cancellationToken);
    }

    public Task<RasGateStatus> GetShadowStatusAsync(
        Guid rasGateId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasGateId);
        return SendAndMapAsync<RasGateStatusResponse, RasGateStatus>(
            HttpMethod.Get,
            $"api/v1/ras-gates/{rasGateId:D}/status/shadow",
            null,
            MapStatus,
            cancellationToken);
    }

    public Task<RasGateStatus> RefreshStatusAsync(
        Guid rasGateId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasGateId);
        return SendAndMapAsync<RasGateStatusResponse, RasGateStatus>(
            HttpMethod.Post,
            $"api/v1/ras-gates/{rasGateId:D}/status/live",
            null,
            MapStatus,
            cancellationToken);
    }

    private async Task<TOutput> SendAndMapAsync<TContract, TOutput>(
        HttpMethod method,
        string relativePath,
        object? body,
        Func<TContract, TOutput> map,
        CancellationToken cancellationToken)
    {
        var data = await SendAsync<TContract>(
            method,
            relativePath,
            body,
            cancellationToken);

        return map(data);
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string relativePath,
        object? body,
        CancellationToken cancellationToken)
    {
        var connection = connectionProvider.GetRequiredConnection();
        var requestUri = new Uri(connection.BaseAddress, relativePath);
        using var request = new HttpRequestMessage(
            method,
            requestUri);
        var requestPath = requestUri.AbsolutePath;

        logger.LogDebug(
            "Sending RasHub request {RequestMethod} {RequestPath}",
            method.Method,
            requestPath);

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation(ApiKeyHeaderName, connection.ApiKey);

        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "RasHub request {RequestMethod} {RequestPath} timed out",
                method.Method,
                requestPath);
            throw new RasHubApiException(
                "The RasHub request timed out.",
                innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "RasHub request {RequestMethod} {RequestPath} failed during transport",
                method.Method,
                requestPath);
            throw new RasHubApiException(
                "Unable to reach RasHub. Check the URL and network connection.",
                innerException: exception);
        }

        using (response)
        {
            var traceId = GetTraceId(response);
            ApiResponse<T>? envelope;

            try
            {
                await using var responseStream =
                    await response.Content.ReadAsStreamAsync(cancellationToken);
                envelope = await JsonSerializer.DeserializeAsync<ApiResponse<T>>(
                    responseStream,
                    JsonOptions,
                    cancellationToken);
            }
            catch (JsonException exception)
            {
                logger.LogWarning(
                    exception,
                    "RasHub request {RequestMethod} {RequestPath} returned invalid JSON with " +
                    "HTTP {StatusCode} and trace {TraceId}",
                    method.Method,
                    requestPath,
                    (int)response.StatusCode,
                    traceId);
                throw new RasHubApiException(
                    "RasHub returned an invalid JSON response.",
                    response.StatusCode,
                    traceId: traceId,
                    innerException: exception);
            }

            if (envelope is null)
            {
                logger.LogWarning(
                    "RasHub request {RequestMethod} {RequestPath} returned an empty response " +
                    "with HTTP {StatusCode} and trace {TraceId}",
                    method.Method,
                    requestPath,
                    (int)response.StatusCode,
                    traceId);
                throw new RasHubApiException(
                    "RasHub returned an empty response.",
                    response.StatusCode,
                    traceId: traceId);
            }

            if (!response.IsSuccessStatusCode || !envelope.Success)
            {
                var exception = CreateApiException(response.StatusCode, traceId, envelope);
                logger.LogWarning(
                    "RasHub request {RequestMethod} {RequestPath} failed with HTTP " +
                    "{StatusCode}, code {ErrorCode}, and trace {TraceId}",
                    method.Method,
                    requestPath,
                    (int)response.StatusCode,
                    exception.Code,
                    traceId);
                throw exception;
            }

            if (envelope.Data is null)
            {
                logger.LogWarning(
                    "RasHub request {RequestMethod} {RequestPath} returned success without data " +
                    "with HTTP {StatusCode} and trace {TraceId}",
                    method.Method,
                    requestPath,
                    (int)response.StatusCode,
                    traceId);
                throw new RasHubApiException(
                    "RasHub returned a successful response without data.",
                    response.StatusCode,
                    traceId: traceId);
            }

            logger.LogDebug(
                "RasHub request {RequestMethod} {RequestPath} completed with HTTP " +
                "{StatusCode} and trace {TraceId}",
                method.Method,
                requestPath,
                (int)response.StatusCode,
                traceId);

            return envelope.Data;
        }
    }

    private static RasHubApiException CreateApiException<T>(
        HttpStatusCode statusCode,
        string? traceId,
        ApiResponse<T> envelope)
    {
        var error = envelope.Error;
        var validationErrors = envelope.Errors?
            .Select(item => new RasHubApiError(item.Code, item.Message, item.Target))
            .ToArray() ?? [];
        var message = validationErrors.FirstOrDefault()?.Message ??
                      error?.Message ??
                      $"RasHub request failed with HTTP {(int)statusCode}.";

        return new RasHubApiException(
            message,
            statusCode,
            error?.Code,
            error?.Target,
            traceId,
            validationErrors);
    }

    private static string? GetTraceId(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues(TraceIdHeaderName, out var values)
            ? values.FirstOrDefault()
            : null;
    }

    private static RasGatePage MapPage(PageResult<ContractRasGate> page)
    {
        return new RasGatePage(
            page.Items.Select(MapGate).ToArray(),
            page.TotalCount,
            page.Page,
            page.PageSize,
            page.TotalPages);
    }

    private static RasGate MapGate(ContractRasGate gate)
    {
        return new RasGate(
            gate.Id,
            gate.Name,
            gate.Url,
            gate.Port,
            gate.IsActive,
            gate.CreatedAt,
            gate.UpdatedAt);
    }

    private static RasGateStatus MapStatus(RasGateStatusResponse status)
    {
        return new RasGateStatus(
            status.State switch
            {
                ContractHealth.Unknown => RasGateHealth.Unknown,
                ContractHealth.Offline => RasGateHealth.Offline,
                ContractHealth.Degraded => RasGateHealth.Degraded,
                ContractHealth.Ready => RasGateHealth.Ready,
                _ => throw new JsonException($"Unknown RasGate health state: {status.State}.")
            },
            status.InstanceName,
            status.RasGateVersion,
            status.RasGateObservedAt,
            status.RacAvailable,
            status.RacVersion,
            status.RacObservedAt);
    }

    private static string BuildSearchQuery(
        string query,
        IReadOnlyCollection<RasGateSearchField>? fields)
    {
        var values = new List<string> { $"query={Uri.EscapeDataString(query)}" };

        if (fields is not null)
            values.AddRange(fields.Select(field =>
                $"fields={Uri.EscapeDataString(field.ToString())}"));

        return string.Join('&', values);
    }

    private static string? NormalizeQuery(string? query)
    {
        return string.IsNullOrWhiteSpace(query) ? null : query.Trim();
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1)
            throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
    }

    private static void ValidateId(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("RasGate ID must not be empty.", nameof(id));
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }
}
