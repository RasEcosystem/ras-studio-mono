namespace RasStudio.Web.Components;

public sealed record RasGateEditorValues(
    string Name,
    string Url,
    int Port,
    string? ApiKey,
    bool IsActive);
