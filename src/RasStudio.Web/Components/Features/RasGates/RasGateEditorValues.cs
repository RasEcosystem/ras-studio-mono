namespace RasStudio.Web.Components.Features.RasGates;

public sealed record RasGateEditorValues(
    string Name,
    string Url,
    int Port,
    string? ApiKey,
    bool IsActive);
