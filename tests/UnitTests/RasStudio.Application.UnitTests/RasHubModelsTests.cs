using System.Net;
using RasStudio.Application.RasHub;

namespace RasStudio.Application.UnitTests;

public sealed class RasHubModelsTests
{
    [Theory]
    [InlineData("https://hub.example/", true, true)]
    [InlineData("https://hub.example/", false, false)]
    [InlineData("", true, false)]
    [InlineData("   ", true, false)]
    public void ConnectionState_IsConfiguredRequiresUrlAndApiKey(
        string baseUrl,
        bool hasApiKey,
        bool expected)
    {
        var state = new RasHubConnectionState(baseUrl, hasApiKey);

        Assert.Equal(expected, state.IsConfigured);
    }

    [Fact]
    public void ApiException_PreservesHubFailureMetadata()
    {
        var errors = new[] { new RasHubApiError("validation_error", "Name is required", "Name") };

        var exception = new RasHubApiException(
            "Bad request",
            HttpStatusCode.BadRequest,
            "bad_request",
            "request",
            "trace-123",
            errors);

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal("bad_request", exception.Code);
        Assert.Equal("request", exception.Target);
        Assert.Equal("trace-123", exception.TraceId);
        Assert.Same(errors, exception.Errors);
    }
}
