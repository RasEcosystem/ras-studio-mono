using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RasHub.Contracts.Common;
using RasStudio.Application.RasHub;

namespace RasStudio.Infrastructure.RasHub;

public sealed class RasHubApiClient(
    HttpClient httpClient,
    IRasHubConnectionProvider connectionProvider,
    ILogger<RasHubApiClient> logger)
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string TraceIdHeaderName = "X-Trace-Id";
    private const long MaxResponseContentBytes = 16 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<TOutput> SendAndMapAsync<TContract, TOutput>(
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

        try
        {
            return map(data);
        }
        catch (Exception exception) when (exception is
                                              JsonException or
                                              InvalidOperationException or
                                              ArgumentException or
                                              NullReferenceException)
        {
            throw new RasHubApiException(
                "RasHub returned incompatible response data.",
                innerException: exception);
        }
    }

    public async Task<T> SendAsync<T>(
        HttpMethod method,
        string relativePath,
        object? body,
        CancellationToken cancellationToken)
    {
        var connection = connectionProvider.GetRequiredConnection();
        return await SendAsync<T>(
            method,
            relativePath,
            body,
            connection,
            cancellationToken);
    }

    internal async Task<T> SendAsync<T>(
        HttpMethod method,
        string relativePath,
        object? body,
        RasHubConnection connection,
        CancellationToken cancellationToken)
    {
        var requestUri = new Uri(connection.BaseAddress, relativePath);
        using var request = new HttpRequestMessage(method, requestUri);
        var requestPath = requestUri.AbsolutePath;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);

        if (httpClient.Timeout != Timeout.InfiniteTimeSpan)
            deadline.CancelAfter(httpClient.Timeout);

        var requestToken = deadline.Token;

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
                requestToken);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "RasHub request {RequestMethod} {RequestPath} timed out",
                method.Method,
                requestPath);
            throw new RasHubApiException(
                "The RasHub request timed out. Its outcome may be unknown; reload data " +
                "before retrying.",
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
                if (response.Content.Headers.ContentLength is > MaxResponseContentBytes)
                    throw ResponseTooLarge(response.StatusCode, traceId);

                await response.Content.LoadIntoBufferAsync(
                    MaxResponseContentBytes,
                    requestToken);
                await using var responseStream =
                    await response.Content.ReadAsStreamAsync(requestToken);
                envelope = await JsonSerializer.DeserializeAsync<ApiResponse<T>>(
                    responseStream,
                    JsonOptions,
                    requestToken);
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
            catch (OperationCanceledException exception)
                when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    exception,
                    "RasHub request {RequestMethod} {RequestPath} timed out while reading " +
                    "the response body with HTTP {StatusCode} and trace {TraceId}",
                    method.Method,
                    requestPath,
                    (int)response.StatusCode,
                    traceId);
                throw new RasHubApiException(
                    "The RasHub request timed out. Its outcome may be unknown; reload data " +
                    "before retrying.",
                    response.StatusCode,
                    traceId: traceId,
                    innerException: exception);
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException)
            {
                logger.LogWarning(
                    exception,
                    "RasHub request {RequestMethod} {RequestPath} returned an unreadable or " +
                    "oversized body with HTTP {StatusCode} and trace {TraceId}",
                    method.Method,
                    requestPath,
                    (int)response.StatusCode,
                    traceId);
                throw new RasHubApiException(
                    "The RasHub response could not be read or exceeded the supported size.",
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

    private static RasHubApiException ResponseTooLarge(
        HttpStatusCode statusCode,
        string? traceId)
    {
        return new RasHubApiException(
            "The RasHub response exceeded the supported size.",
            statusCode,
            traceId: traceId);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }
}
