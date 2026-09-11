namespace RasStudio.Web.Components.Features.RasEndpoints;

public sealed record RasEndpointEditorValues(
    string Name,
    Guid RasGateId,
    string Host,
    int Port,
    bool IsActive);
