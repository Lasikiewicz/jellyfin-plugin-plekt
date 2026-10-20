using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Plekt;

/// <summary>
/// Plekt connection settings.
/// </summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the complete webhook address copied from Plekt.
    /// </summary>
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the legacy base URL used by earlier plugin versions.
    /// </summary>
    public string PlektUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the legacy webhook secret used by earlier plugin versions.
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;
}
