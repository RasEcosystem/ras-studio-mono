using Nava.Settings;

namespace RasStudio.Application.Settings;

/// <summary>Persistent user-facing application settings.</summary>
[SettingsKey("app-settings")]
public sealed class ApplicationSettings
{
    public AppTheme Theme { get; set; } = AppTheme.RasStudioMono;

    public string InferenceServerUrl { get; set; } = "http://127.0.0.1:11434";

    public string InferenceModel { get; set; } = "Qwen3:latest";
}
