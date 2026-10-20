using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Plekt;

/// <summary>
/// Plekt connection settings.
/// </summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the base URL used to reach Plekt from Jellyfin.
    /// </summary>
    public string PlektUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Plekt webhook secret.
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;
}
