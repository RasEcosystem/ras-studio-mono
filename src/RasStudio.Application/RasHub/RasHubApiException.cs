using System.Net;

namespace RasStudio.Application.RasHub;

public sealed record RasHubApiError(
    string Code,
    string Message,
    string? Target = null);

public sealed class RasHubApiException : Exception
{
    public RasHubApiException(
        string message,
        HttpStatusCode? statusCode = null,
        string? code = null,
        string? target = null,
        string? traceId = null,
        IReadOnlyList<RasHubApiError>? errors = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Code = code;
        Target = target;
        TraceId = traceId;
        Errors = errors ?? [];
    }

    public HttpStatusCode? StatusCode { get; }

    public string? Code { get; }

    public string? Target { get; }

    public string? TraceId { get; }

    public IReadOnlyList<RasHubApiError> Errors { get; }
}
