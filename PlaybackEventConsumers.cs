using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Plekt;

/// <summary>Forwards playback-start events.</summary>
public sealed class PlaybackStartConsumer : IEventConsumer<PlaybackStartEventArgs>
{
    private readonly ILogger<PlaybackStartConsumer> _logger;

    /// <summary>Creates a playback-start event consumer.</summary>
    public PlaybackStartConsumer(ILogger<PlaybackStartConsumer> logger) => _logger = logger;

    /// <inheritdoc />
    public Task OnEvent(PlaybackStartEventArgs eventArgs) => PlektWebhookSender.SendPlaybackAsync(
        _logger, "PlaybackStart", eventArgs.Item, eventArgs.Users, eventArgs.PlaybackPositionTicks,
        device: eventArgs.DeviceName, deviceId: eventArgs.DeviceId, client: eventArgs.ClientName,
        sessionId: eventArgs.PlaySessionId);
}

/// <summary>Forwards playback-progress events.</summary>
public sealed class PlaybackProgressConsumer : IEventConsumer<PlaybackProgressEventArgs>
{
    private readonly ILogger<PlaybackProgressConsumer> _logger;

    /// <summary>Creates a playback-progress event consumer.</summary>
    public PlaybackProgressConsumer(ILogger<PlaybackProgressConsumer> logger) => _logger = logger;

    /// <inheritdoc />
    public Task OnEvent(PlaybackProgressEventArgs eventArgs) => PlektWebhookSender.SendPlaybackAsync(
        _logger, "PlaybackProgress", eventArgs.Item, eventArgs.Users, eventArgs.PlaybackPositionTicks,
        device: eventArgs.DeviceName, deviceId: eventArgs.DeviceId, client: eventArgs.ClientName,
        sessionId: eventArgs.PlaySessionId);
}

/// <summary>Forwards playback-stop events.</summary>
public sealed class PlaybackStopConsumer : IEventConsumer<PlaybackStopEventArgs>
{
    private readonly ILogger<PlaybackStopConsumer> _logger;

    /// <summary>Creates a playback-stop event consumer.</summary>
    public PlaybackStopConsumer(ILogger<PlaybackStopConsumer> logger) => _logger = logger;

    /// <inheritdoc />
    public Task OnEvent(PlaybackStopEventArgs eventArgs) => PlektWebhookSender.SendPlaybackAsync(
        _logger, "PlaybackStop", eventArgs.Item, eventArgs.Users, eventArgs.PlaybackPositionTicks,
        eventArgs.PlayedToCompletion, eventArgs.DeviceName, eventArgs.DeviceId, eventArgs.ClientName,
        eventArgs.PlaySessionId);
}

/// <summary>Forwards explicit watched and unwatched changes.</summary>
public sealed class UserDataSaveConsumer : IEventConsumer<UserDataSaveEventArgs>
{
    private readonly ILogger<UserDataSaveConsumer> _logger;

    /// <summary>Creates a user-data-save event consumer.</summary>
    public UserDataSaveConsumer(ILogger<UserDataSaveConsumer> logger) => _logger = logger;

    /// <inheritdoc />
    public Task OnEvent(UserDataSaveEventArgs eventArgs) => PlektWebhookSender.SendUserDataAsync(eventArgs, _logger);
}
