using MediaBrowser.Controller;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Plekt;

/// <summary>
/// Registers Plekt's Jellyfin event consumers.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddScoped<IEventConsumer<PlaybackStartEventArgs>, PlaybackStartConsumer>();
        serviceCollection.AddScoped<IEventConsumer<PlaybackProgressEventArgs>, PlaybackProgressConsumer>();
        serviceCollection.AddScoped<IEventConsumer<PlaybackStopEventArgs>, PlaybackStopConsumer>();
        serviceCollection.AddScoped<IEventConsumer<UserDataSaveEventArgs>, UserDataSaveConsumer>();
    }
}
