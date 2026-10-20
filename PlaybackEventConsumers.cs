using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Plekt;

/// <summary>Forwards playback-start events.</summary>
public sealed class PlaybackStartConsumer : IEventConsumer<PlaybackStartEventArgs>
{
    private readonly ILogger<PlaybackStartConsumer> _logger;
    private readonly PlektWebhookSender _sender;

    /// <summary>Creates a playback-start event consumer.</summary>
    public PlaybackStartConsumer(ILogger<PlaybackStartConsumer> logger, PlektWebhookSender sender)
    {
        _logger = logger;
        _sender = sender;
    }

    /// <inheritdoc />
    public Task OnEvent(PlaybackStartEventArgs eventArgs) => _sender.SendPlaybackAsync(
        _logger, "PlaybackStart", eventArgs.Item, eventArgs.Users, eventArgs.PlaybackPositionTicks,
        device: eventArgs.DeviceName, deviceId: eventArgs.DeviceId, client: eventArgs.ClientName,
        sessionId: eventArgs.PlaySessionId);
}

/// <summary>Forwards playback-progress events.</summary>
public sealed class PlaybackProgressConsumer : IEventConsumer<PlaybackProgressEventArgs>
{
    private readonly ILogger<PlaybackProgressConsumer> _logger;
    private readonly PlektWebhookSender _sender;

    /// <summary>Creates a playback-progress event consumer.</summary>
    public PlaybackProgressConsumer(ILogger<PlaybackProgressConsumer> logger, PlektWebhookSender sender)
    {
        _logger = logger;
        _sender = sender;
    }

    /// <inheritdoc />
    public Task OnEvent(PlaybackProgressEventArgs eventArgs) => _sender.SendPlaybackAsync(
        _logger, "PlaybackProgress", eventArgs.Item, eventArgs.Users, eventArgs.PlaybackPositionTicks,
        device: eventArgs.DeviceName, deviceId: eventArgs.DeviceId, client: eventArgs.ClientName,
        sessionId: eventArgs.PlaySessionId);
}

/// <summary>Forwards playback-stop events.</summary>
public sealed class PlaybackStopConsumer : IEventConsumer<PlaybackStopEventArgs>
{
    private readonly ILogger<PlaybackStopConsumer> _logger;
    private readonly PlektWebhookSender _sender;

    /// <summary>Creates a playback-stop event consumer.</summary>
    public PlaybackStopConsumer(ILogger<PlaybackStopConsumer> logger, PlektWebhookSender sender)
    {
        _logger = logger;
        _sender = sender;
    }

    /// <inheritdoc />
    public Task OnEvent(PlaybackStopEventArgs eventArgs) => _sender.SendPlaybackAsync(
        _logger, "PlaybackStop", eventArgs.Item, eventArgs.Users, eventArgs.PlaybackPositionTicks,
        eventArgs.PlayedToCompletion, eventArgs.DeviceName, eventArgs.DeviceId, eventArgs.ClientName,
        eventArgs.PlaySessionId);
}

/// <summary>Forwards explicit watched and unwatched changes.</summary>
/// <remarks>
/// Jellyfin raises user data saves as an <see cref="IUserDataManager.UserDataSaved"/> event,
/// not through the event manager, so an <c>IEventConsumer</c> never receives them.
/// </remarks>
public sealed class UserDataSaveNotifier : IHostedService
{
    private readonly ILogger<UserDataSaveNotifier> _logger;
    private readonly PlektWebhookSender _sender;
    private readonly IUserDataManager _userDataManager;

    /// <summary>Creates a user-data-save notifier.</summary>
    public UserDataSaveNotifier(ILogger<UserDataSaveNotifier> logger, PlektWebhookSender sender, IUserDataManager userDataManager)
    {
        _logger = logger;
        _sender = sender;
        _userDataManager = userDataManager;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _userDataManager.UserDataSaved += OnUserDataSaved;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _userDataManager.UserDataSaved -= OnUserDataSaved;
        return Task.CompletedTask;
    }

    private async void OnUserDataSaved(object? sender, UserDataSaveEventArgs eventArgs)
    {
        try
        {
            await _sender.SendUserDataAsync(eventArgs, _logger).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // An async void handler must not throw into Jellyfin's save path.
            _logger.LogWarning("Plekt webhook delivery failed (unexpected error)");
        }
    }
}
