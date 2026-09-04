namespace RasStudio.Web.Components;

public sealed record RasEndpointEditorValues(
    string Name,
    Guid RasGateId,
    string Host,
    int Port,
    bool IsActive);
