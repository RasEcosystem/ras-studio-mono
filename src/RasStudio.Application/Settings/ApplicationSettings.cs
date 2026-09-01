using Nava.Settings;

namespace RasStudio.Application.Settings;

/// <summary>Persistent user-facing application settings.</summary>
[SettingsKey("app-settings")]
public sealed class ApplicationSettings
{
    public AppTheme Theme { get; set; }

    public string InferenceServerUrl { get; set; } = "http://192.168.253.25:11434";

    public string InferenceModel { get; set; } = "Qwen3:latest";
}
