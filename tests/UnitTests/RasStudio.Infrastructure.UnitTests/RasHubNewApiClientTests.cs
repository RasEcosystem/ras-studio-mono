using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using RasStudio.Application.RasEndpoints;
using RasStudio.Application.RasHub;
using RasStudio.Infrastructure.RasHub;

namespace RasStudio.Infrastructure.UnitTests;

public sealed class RasHubNewApiClientTests
{
    private const string UserApiKey = "0123456789abcdef0123456789abcdef";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task RasEndpointQueries_UseNewRoutesAndMapConfigurationRevision()
    {
        var handler = new RecordingHandler(
            Success(new { items = new[] { EndpointModel() }, totalCount = 11, page = 2, pageSize = 10 }),
            Success(new[] { EndpointModel() }),
            Success(EndpointModel()));
        var client = new RasHubRasEndpointClient(CreateApiClient(handler));
        var id = Guid.Parse("a42195a1-b54d-4593-b56b-b4fe2d1da355");

        var page = await client.GetPageAsync(
            2,
            10,
            TestContext.Current.CancellationToken);
        var all = await client.GetAllAsync(TestContext.Current.CancellationToken);
        var endpoint = await client.GetAsync(id, TestContext.Current.CancellationToken);

        Assert.Equal(11, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Single(all);
        Assert.Equal(13, endpoint.ConfigurationRevision);
        Assert.Equal("ras.example.test", endpoint.Host);
        Assert.All(handler.Requests, request => Assert.Equal(UserApiKey, request.ApiKey));
        Assert.Collection(
            handler.Requests,
            request => Assert.Equal(
                "/root/api/v1/ras-endpoints?page=2&pageSize=10",
                request.PathAndQuery),
            request => Assert.Equal(
                "/root/api/v1/ras-endpoints/all",
                request.PathAndQuery),
            request => Assert.Equal(
                $"/root/api/v1/ras-endpoints/{id:D}",
                request.PathAndQuery));
    }

    [Fact]
    public async Task RasEndpointMutations_SendGateAssignmentAndExpectedRevision()
    {
        var handler = new RecordingHandler(
            Success(EndpointModel(), HttpStatusCode.Created),
            Success(EndpointModel()),
            Success(EndpointModel()));
        var client = new RasHubRasEndpointClient(CreateApiClient(handler));
        var endpointId = Guid.Parse("a42195a1-b54d-4593-b56b-b4fe2d1da355");
        var gateId = Guid.Parse("f3648378-d27b-482b-bb49-61f6149d3574");

        await client.CreateAsync(
            new CreateRasEndpoint("Production RAS", gateId, "ras.example.test", 1545, true),
            TestContext.Current.CancellationToken);
        await client.UpdateAsync(
            endpointId,
            new UpdateRasEndpoint(
                "Production RAS",
                gateId,
                "ras.example.test",
                1545,
                false,
                13),
            TestContext.Current.CancellationToken);
        await client.DeleteAsync(endpointId, TestContext.Current.CancellationToken);

        Assert.Collection(
            handler.Requests,
            create =>
            {
                Assert.Equal(HttpMethod.Post, create.Method);
                Assert.Equal("/root/api/v1/ras-endpoints", create.PathAndQuery);
                using var json = JsonDocument.Parse(create.Body!);
                Assert.Equal(gateId, json.RootElement.GetProperty("rasGateId").GetGuid());
                Assert.Equal("ras.example.test", json.RootElement.GetProperty("host").GetString());
            },
            update =>
            {
                Assert.Equal(HttpMethod.Put, update.Method);
                Assert.Equal($"/root/api/v1/ras-endpoints/{endpointId:D}", update.PathAndQuery);
                using var json = JsonDocument.Parse(update.Body!);
                Assert.False(json.RootElement.GetProperty("isActive").GetBoolean());
                Assert.Equal(
                    13,
                    json.RootElement.GetProperty("expectedConfigurationRevision").GetInt64());
            },
            delete =>
            {
                Assert.Equal(HttpMethod.Delete, delete.Method);
                Assert.Equal($"/root/api/v1/ras-endpoints/{endpointId:D}", delete.PathAndQuery);
                Assert.Null(delete.Body);
            });
    }

    [Fact]
    public async Task ClusterClient_UsesEndpointScopedShadowRoutes()
    {
        var handler = new RecordingHandler(
            Success(new { items = new[] { ClusterModel() }, totalCount = 1, page = 1, pageSize = 25 }),
            Success(new { totalCount = 1, observedAt = "2026-09-04T13:00:00Z" }));
        var client = new RasHubClusterClient(CreateApiClient(handler));
        var endpointId = Guid.Parse("a42195a1-b54d-4593-b56b-b4fe2d1da355");

        var page = await client.GetShadowPageAsync(
            endpointId,
            1,
            25,
            TestContext.Current.CancellationToken);
        var refresh = await client.RefreshShadowAsync(
            endpointId,
            TestContext.Current.CancellationToken);

        var cluster = Assert.Single(page.Items);
        Assert.Equal("Cluster One", cluster.Name);
        Assert.Equal(1, refresh.TotalCount);
        Assert.Collection(
            handler.Requests,
            request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal(
                    $"/root/api/v1/ras-endpoints/{endpointId:D}/clusters/shadow?page=1&pageSize=25",
                    request.PathAndQuery);
            },
            request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal(
                    $"/root/api/v1/ras-endpoints/{endpointId:D}/clusters/shadow/refresh",
                    request.PathAndQuery);
            });
    }

    [Fact]
    public async Task InfoClient_UsesPublicInfoRoute()
    {
        var handler = new RecordingHandler(Success(new { version = "0.1.1+abcdef" }));
        var client = new RasHubInfoClient(CreateApiClient(handler));

        var info = await client.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal("0.1.1+abcdef", info.Version);
        Assert.Equal("/root/api/v1/info", handler.Request!.PathAndQuery);
    }

    [Fact]
    public async Task ApiClient_timeout_covers_response_body()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new NeverEndingContent() };
        var client = CreateApiClient(
            new RecordingHandler(response),
            TimeSpan.FromMilliseconds(50));

        var exception = await Assert.ThrowsAsync<RasHubApiException>(() =>
            client.SendAsync<object>(
                HttpMethod.Get,
                "api/v1/test",
                null,
                CancellationToken.None));

        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("outcome may be unknown", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApiClient_rejects_declared_oversized_response_before_reading_body()
    {
        var response = Success(new { value = true });
        response.Content.Headers.ContentLength = 16 * 1024 * 1024 + 1;
        response.Headers.Add("X-Trace-Id", "trace-large");
        var client = CreateApiClient(new RecordingHandler(response));

        var exception = await Assert.ThrowsAsync<RasHubApiException>(() =>
            client.SendAsync<object>(
                HttpMethod.Get,
                "api/v1/test",
                null,
                TestContext.Current.CancellationToken));

        Assert.Contains("exceeded", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("trace-large", exception.TraceId);
    }

    [Fact]
    public async Task Mapping_invalid_success_payload_returns_protocol_exception()
    {
        var handler = new RecordingHandler(Success(new
        {
            items = (object?)null,
            totalCount = 1,
            page = 1,
            pageSize = 10
        }));
        var client = new RasHubRasEndpointClient(CreateApiClient(handler));

        var exception = await Assert.ThrowsAsync<RasHubApiException>(() =>
            client.GetPageAsync(
                1,
                10,
                TestContext.Current.CancellationToken));

        Assert.Contains("incompatible", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static RasHubApiClient CreateApiClient(
        RecordingHandler handler,
        TimeSpan? timeout = null)
    {
        return new RasHubApiClient(
            new HttpClient(handler) { Timeout = timeout ?? TimeSpan.FromSeconds(5) },
            new StaticConnectionProvider(),
            NullLogger<RasHubApiClient>.Instance);
    }

    private static HttpResponseMessage Success(
        object data,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { success = true, data }, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };
    }

    private static object EndpointModel()
    {
        return new
        {
            id = "a42195a1-b54d-4593-b56b-b4fe2d1da355",
            rasGateId = "f3648378-d27b-482b-bb49-61f6149d3574",
            name = "Production RAS",
            host = "ras.example.test",
            port = 1545,
            isActive = true,
            lastSeenAt = "2026-09-04T12:00:00Z",
            configurationRevision = 13,
            createdAt = "2026-09-04T10:00:00Z",
            updatedAt = "2026-09-04T11:00:00Z"
        };
    }

    private static object ClusterModel()
    {
        return new
        {
            id = "5c2d3c64-b7bb-4458-a0eb-29bfec638767",
            name = "Cluster One",
            host = "cluster.example.test",
            port = 1541,
            expirationTimeoutSeconds = 0,
            lifetimeLimitSeconds = 0,
            maxMemorySizeKb = 0,
            maxMemoryTimeLimitSeconds = 0,
            securityLevel = 0,
            sessionFaultToleranceLevel = 0,
            loadBalancingMode = "Performance",
            errorsCountThresholdPercent = 0,
            killProblemProcesses = false,
            killByMemoryWithDump = (bool?)null,
            allowAccessRightAuditEventsRecording = (bool?)null,
            pingPeriod = (long?)null,
            pingTimeout = (long?)null,
            restartSchedule = (string?)null,
            observedAt = "2026-09-04T12:30:00Z"
        };
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }

    private sealed class StaticConnectionProvider : IRasHubConnectionProvider
    {
        public RasHubConnection GetRequiredConnection()
        {
            return new RasHubConnection(
                new Uri("https://hub.example/root/"),
                UserApiKey);
        }
    }

    private sealed class RecordingHandler(params HttpResponseMessage[] responses)
        : HttpMessageHandler
    {
        private readonly List<CapturedRequest> _requests = [];
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public IReadOnlyList<CapturedRequest> Requests => _requests;

        public CapturedRequest? Request => _requests.LastOrDefault();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var apiKey = request.Headers.TryGetValues("X-Api-Key", out var values)
                ? values.Single()
                : null;

            _requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri!.PathAndQuery,
                apiKey,
                body));

            return _responses.Dequeue();
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        string PathAndQuery,
        string? ApiKey,
        string? Body);

    private sealed class NeverEndingContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context)
        {
            return Task.Delay(Timeout.InfiniteTimeSpan);
        }

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken)
        {
            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
