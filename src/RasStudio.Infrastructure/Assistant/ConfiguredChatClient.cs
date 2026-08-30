using System.ClientModel;
using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Nava.Settings.Abstractions;
using OpenAI;
using OpenAI.Chat;
using RasStudio.Application.Settings;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace RasStudio.Infrastructure.Assistant;

public sealed class ConfiguredChatClient(
    ISettingsProvider<ApplicationSettings> settingsProvider,
    ILoggerFactory loggerFactory) : IChatClient
{
    private const string OllamaApiKey = "ollama";

    private readonly ConcurrentDictionary<ClientConfiguration, Lazy<IChatClient>> _clients = [];
    private bool _disposed;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return GetClient().GetResponseAsync(messages, options, cancellationToken);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return GetClient().GetStreamingResponseAsync(messages, options, cancellationToken);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        return serviceType == typeof(IChatClient) || serviceType == typeof(ConfiguredChatClient)
            ? this
            : GetClient().GetService(serviceType, serviceKey);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        foreach (var client in _clients.Values)
            if (client.IsValueCreated)
                client.Value.Dispose();

        _clients.Clear();
    }

    private IChatClient GetClient()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var settings = settingsProvider.Settings;
        var configuration = new ClientConfiguration(
            CreateApiEndpoint(settings.InferenceServerUrl),
            GetModel(settings.InferenceModel));

        return _clients.GetOrAdd(
            configuration,
            config => new Lazy<IChatClient>(
                () => CreateClient(config, loggerFactory),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private static IChatClient CreateClient(
        ClientConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        return new ChatClient(
                configuration.Model,
                new ApiKeyCredential(OllamaApiKey),
                new OpenAIClientOptions { Endpoint = configuration.Endpoint })
            .AsIChatClient()
            .AsBuilder()
            .UseFunctionInvocation(
                loggerFactory,
                functionInvocation =>
                {
                    functionInvocation.AllowConcurrentInvocation = false;
                    functionInvocation.IncludeDetailedErrors = false;
                    functionInvocation.MaximumConsecutiveErrorsPerRequest = 1;
                    functionInvocation.MaximumIterationsPerRequest = 6;
                })
            .Build();
    }

    internal static Uri CreateApiEndpoint(string serverUrl)
    {
        if (!Uri.TryCreate(serverUrl?.Trim(), UriKind.Absolute, out var serverUri) ||
            (serverUri.Scheme != Uri.UriSchemeHttp && serverUri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("A valid inference server URL is not configured.");

        var builder = new UriBuilder(serverUri);
        var path = builder.Path.TrimEnd('/');

        if (!path.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) path += "/v1";

        builder.Path = path;
        builder.Query = string.Empty;
        builder.Fragment = string.Empty;

        return builder.Uri;
    }

    internal static string GetModel(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("An inference model is not configured.");

        return model.Trim();
    }

    private sealed record ClientConfiguration(Uri Endpoint, string Model);
}
