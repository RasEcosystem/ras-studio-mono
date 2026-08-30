using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RasStudio.Application.RasGates;
using RasStudio.Application.RasHub;
using RasStudio.Infrastructure.RasHub;

namespace RasStudio.Infrastructure.UnitTests;

public sealed class RasHubRasGateClientTests
{
    private const string UserApiKey = "0123456789abcdef0123456789abcdef";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task GetPageAsync_UsesApiKeyAndMapsPagedResponse()
    {
        var handler = new RecordingHandler(
            Success(new { items = new[] { GateModel() }, totalCount = 11, page = 2, pageSize = 10 }));
        var client = CreateClient(handler);

        var result = await client.GetPageAsync(
            2,
            10,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(11, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal("Gate One", Assert.Single(result.Items).Name);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal("/root/api/v1/ras-gates?page=2&pageSize=10", handler.Request.PathAndQuery);
        Assert.Equal(UserApiKey, handler.Request.ApiKey);
    }

    [Fact]
    public async Task GetPageAsync_WithSearch_UsesBothRequestedFields()
    {
        var handler = new RecordingHandler(
            Success(new { items = Array.Empty<object>(), totalCount = 0, page = 1, pageSize = 25 }));
        var client = CreateClient(handler);

        await client.GetPageAsync(
            1,
            25,
            "gate one",
            [RasGateSearchField.Name, RasGateSearchField.Url],
            TestContext.Current.CancellationToken);

        var path = handler.Request!.PathAndQuery;
        Assert.StartsWith("/root/api/v1/ras-gates/search?", path);
        Assert.Contains("query=gate%20one", path);
        Assert.Contains("fields=Name", path);
        Assert.Contains("fields=Url", path);
        Assert.Contains("page=1&pageSize=25", path);
    }

    [Fact]
    public async Task QueryOperations_UseGetOneAndBothAllRoutes()
    {
        var handler = new RecordingHandler(
            Success(new[] { GateModel() }),
            Success(GateModel()),
            Success(new[] { GateModel() }));
        var client = CreateClient(handler);
        var id = Guid.Parse("f3648378-d27b-482b-bb49-61f6149d3574");

        await client.GetAllAsync(cancellationToken: TestContext.Current.CancellationToken);
        await client.GetAsync(id, TestContext.Current.CancellationToken);
        await client.GetAllAsync(
            "Gate",
            [RasGateSearchField.Name],
            TestContext.Current.CancellationToken);

        Assert.Collection(
            handler.Requests,
            request => Assert.Equal("/root/api/v1/ras-gates/all", request.PathAndQuery),
            request => Assert.Equal($"/root/api/v1/ras-gates/{id:D}", request.PathAndQuery),
            request =>
            {
                Assert.StartsWith("/root/api/v1/ras-gates/search/all?", request.PathAndQuery);
                Assert.Contains("query=Gate", request.PathAndQuery);
                Assert.Contains("fields=Name", request.PathAndQuery);
            });
    }

    [Fact]
    public async Task Mutations_UseExpectedMethodsRoutesAndBodies()
    {
        var handler = new RecordingHandler(
            Success(GateModel(), HttpStatusCode.Created),
            Success(GateModel()),
            Success(GateModel()));
        var client = CreateClient(handler);
        var id = Guid.Parse("f3648378-d27b-482b-bb49-61f6149d3574");
        var gateApiKey = new string('k', 32);

        await client.CreateAsync(new CreateRasGate(
                "Gate One",
                "http://gate.example",
                5050,
                gateApiKey,
                true),
            TestContext.Current.CancellationToken);
        await client.UpdateAsync(id,
            new UpdateRasGate(
                "Gate One",
                "http://gate.example",
                5050,
                false),
            TestContext.Current.CancellationToken);
        await client.DeleteAsync(id, TestContext.Current.CancellationToken);

        Assert.Collection(
            handler.Requests,
            create =>
            {
                Assert.Equal(HttpMethod.Post, create.Method);
                Assert.Equal("/root/api/v1/ras-gates", create.PathAndQuery);
                using var json = JsonDocument.Parse(create.Body!);
                Assert.Equal(gateApiKey, json.RootElement.GetProperty("apiKey").GetString());
                Assert.True(json.RootElement.GetProperty("isActive").GetBoolean());
            },
            update =>
            {
                Assert.Equal(HttpMethod.Put, update.Method);
                Assert.Equal($"/root/api/v1/ras-gates/{id:D}", update.PathAndQuery);
                using var json = JsonDocument.Parse(update.Body!);
                Assert.False(json.RootElement.GetProperty("isActive").GetBoolean());
                Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("apiKey").ValueKind);
            },
            delete =>
            {
                Assert.Equal(HttpMethod.Delete, delete.Method);
                Assert.Equal($"/root/api/v1/ras-gates/{id:D}", delete.PathAndQuery);
                Assert.Null(delete.Body);
            });
    }

    [Fact]
    public async Task StatusOperations_MapStringEnumsAndUseShadowAndLiveRoutes()
    {
        var response = Success(new
        {
            state = "Ready",
            instanceName = "gate-prod",
            rasGateVersion = "0.2.1",
            rasGateObservedAt = "2026-08-27T12:00:00Z",
            racAvailable = true,
            racVersion = "8.3.27.2214",
            racObservedAt = "2026-08-27T12:00:01Z"
        });
        var handler = new RecordingHandler(response, Clone(response));
        var client = CreateClient(handler);
        var id = Guid.Parse("f3648378-d27b-482b-bb49-61f6149d3574");

        var shadow = await client.GetShadowStatusAsync(
            id,
            TestContext.Current.CancellationToken);
        var live = await client.RefreshStatusAsync(
            id,
            TestContext.Current.CancellationToken);

        Assert.Equal(RasGateHealth.Ready, shadow.State);
        Assert.True(live.RacAvailable);
        Assert.Collection(
            handler.Requests,
            request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.EndsWith($"/{id:D}/status/shadow", request.PathAndQuery);
            },
            request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.EndsWith($"/{id:D}/status/live", request.PathAndQuery);
            });
    }

    [Fact]
    public async Task ErrorEnvelope_PreservesHttpStatusTraceAndValidationDetails()
    {
        var response = JsonResponse(
            HttpStatusCode.BadRequest,
            new
            {
                success = false,
                error = new { code = "bad_request", message = "Bad request" },
                errors = new[] { new { code = "validation_error", message = "Name is required", target = "Name" } }
            });
        response.Headers.Add("X-Trace-Id", "trace-123");
        var client = CreateClient(new RecordingHandler(response));

        var exception = await Assert.ThrowsAsync<RasHubApiException>(() =>
            client.GetPageAsync(
                1,
                10,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal("bad_request", exception.Code);
        Assert.Equal("trace-123", exception.TraceId);
        var validation = Assert.Single(exception.Errors);
        Assert.Equal("Name", validation.Target);
        Assert.Equal("Name is required", validation.Message);
    }

    [Fact]
    public async Task FailureLogging_ContainsOperationalMetadataButNeverSecretsOrQueryValues()
    {
        var response = JsonResponse(
            HttpStatusCode.ServiceUnavailable,
            new { success = false, error = new { code = "hub_unavailable", message = "Unavailable" } });
        response.Headers.Add("X-Trace-Id", "trace-456");
        var logger = new RecordingLogger<RasHubRasGateClient>();
        var client = CreateClient(new RecordingHandler(response), logger);

        await Assert.ThrowsAsync<RasHubApiException>(() =>
            client.GetPageAsync(
                1,
                10,
                "secret search",
                cancellationToken: TestContext.Current.CancellationToken));

        var log = string.Join(Environment.NewLine, logger.Messages);
        Assert.Contains("503", log);
        Assert.Contains("hub_unavailable", log);
        Assert.Contains("trace-456", log);
        Assert.Contains("/root/api/v1/ras-gates/search", log);
        Assert.DoesNotContain(UserApiKey, log);
        Assert.DoesNotContain("secret search", log);
        Assert.DoesNotContain("hub.example", log);
    }

    private static RasHubRasGateClient CreateClient(
        RecordingHandler handler,
        ILogger<RasHubRasGateClient>? logger = null)
    {
        var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        return new RasHubRasGateClient(
            httpClient,
            new StaticConnectionProvider(),
            logger ?? NullLogger<RasHubRasGateClient>.Instance);
    }

    private static HttpResponseMessage Success(
        object data,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return JsonResponse(statusCode, new { success = true, data });
    }

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        object body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };
    }

    private static HttpResponseMessage Clone(HttpResponseMessage response)
    {
        var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        return new HttpResponseMessage(response.StatusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
    }

    private static object GateModel()
    {
        return new
        {
            id = "f3648378-d27b-482b-bb49-61f6149d3574",
            name = "Gate One",
            url = "http://gate.example",
            port = 5050,
            isActive = true,
            createdAt = "2026-08-27T10:00:00Z",
            updatedAt = "2026-08-27T11:00:00Z"
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
}
