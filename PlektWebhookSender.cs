using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Plekt;

/// <summary>
/// Creates Jellyfin Webhook plugin-compatible payloads and posts them to Plekt.
/// </summary>
public sealed class PlektWebhookSender
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = null };
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastProgressSent = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PlektWebhookSender"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Jellyfin's HTTP client factory.</param>
    public PlektWebhookSender(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    /// <summary>
    /// Posts a playback event for each user to Plekt.
    /// </summary>
    /// <param name="logger">Logger for delivery failures.</param>
    /// <param name="notificationType">The Jellyfin Webhook-style notification type.</param>
    /// <param name="item">The played item.</param>
    /// <param name="users">The users the event applies to.</param>
    /// <param name="positionTicks">The playback position in ticks.</param>
    /// <param name="playedToCompletion">Whether playback reached completion.</param>
    /// <param name="device">The device name.</param>
    /// <param name="deviceId">The device identifier.</param>
    /// <param name="client">The client application name.</param>
    /// <param name="sessionId">The play session identifier.</param>
    /// <returns>A task that completes when delivery has been attempted.</returns>
    public async Task SendPlaybackAsync(
        ILogger logger,
        string notificationType,
        BaseItem? item,
        IEnumerable<User> users,
        long? positionTicks,
        bool? playedToCompletion = null,
        string? device = null,
        string? deviceId = null,
        string? client = null,
        string? sessionId = null)
    {
        if (item is null || Plugin.Instance is null) return;
        foreach (var user in users)
        {
            // Progress events repeat every few seconds per session, so they are throttled.
            // Start and stop events are always sent, so Plekt sees every state change.
            var progressKey = $"{sessionId}|{item.Id}|{user.Id}";
            if (notificationType == "PlaybackProgress" && !ShouldSendProgress(progressKey)) continue;
            if (notificationType == "PlaybackStop") _lastProgressSent.TryRemove(progressKey, out _);

            var payload = CreatePayload(notificationType, item, user.Id, user.Username);
            AddPlaybackFields(payload, positionTicks, playedToCompletion, device, deviceId, client, sessionId);
            await PostAsync(payload, logger).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Posts an explicit watched or unwatched change to Plekt.
    /// </summary>
    /// <param name="eventArgs">The user data save event.</param>
    /// <param name="logger">Logger for delivery failures.</param>
    /// <returns>A task that completes when delivery has been attempted.</returns>
    public async Task SendUserDataAsync(UserDataSaveEventArgs eventArgs, ILogger logger)
    {
        if (Plugin.Instance is null || eventArgs.Item is null) return;

        // Playback progress also saves user data. Only explicit toggles represent a
        // user changing the canonical watched flag.
        if (eventArgs.SaveReason != UserDataSaveReason.TogglePlayed) return;

        var payload = CreatePayload("UserDataSaved", eventArgs.Item, eventArgs.UserId, string.Empty);
        payload["SaveReason"] = eventArgs.SaveReason.ToString();
        payload["Played"] = eventArgs.UserData.Played;
        payload["PlaybackPositionTicks"] = eventArgs.UserData.PlaybackPositionTicks;
        if (eventArgs.UserData.LastPlayedDate.HasValue)
        {
            payload["LastPlayedDate"] = eventArgs.UserData.LastPlayedDate.Value;
        }

        if (await PostAsync(payload, logger).ConfigureAwait(false))
        {
            logger.LogInformation("Plekt watched-state change sent (played: {Played})", eventArgs.UserData.Played);
        }
    }

    private bool ShouldSendProgress(string key)
    {
        var now = DateTimeOffset.UtcNow;
        if (_lastProgressSent.TryGetValue(key, out var last) && now - last < ProgressInterval) return false;

        _lastProgressSent[key] = now;
        if (_lastProgressSent.Count > 500)
        {
            // Forget sessions that have been quiet for an hour, so the map cannot grow without bound.
            foreach (var entry in _lastProgressSent)
            {
                if (now - entry.Value > TimeSpan.FromHours(1)) _lastProgressSent.TryRemove(entry.Key, out _);
            }
        }
        return true;
    }

    private static Dictionary<string, object?> CreatePayload(string notificationType, BaseItem item, Guid userId, string userName)
    {
        var itemData = new Dictionary<string, object?>
        {
            ["Id"] = JellyfinId(item.Id),
            ["Type"] = item.GetType().Name,
            ["Name"] = item.Name,
            ["ProviderIds"] = item.ProviderIds,
            ["RunTimeTicks"] = item.RunTimeTicks,
            ["ProductionYear"] = item.ProductionYear,
            ["PremiereDate"] = item.PremiereDate,
            ["ParentId"] = JellyfinId(item.ParentId),
            ["ParentIndexNumber"] = item.ParentIndexNumber,
            ["IndexNumber"] = item.IndexNumber,
        };

        if (item is Episode episode)
        {
            itemData["SeriesId"] = JellyfinId(episode.SeriesId);
            itemData["SeriesName"] = episode.Series?.Name;
            itemData["SeriesProviderIds"] = episode.Series?.ProviderIds;
            itemData["SeriesProductionYear"] = episode.Series?.ProductionYear;
        }

        return new Dictionary<string, object?>
        {
            ["NotificationType"] = notificationType,
            ["ItemId"] = JellyfinId(item.Id),
            ["ItemType"] = item.GetType().Name,
            ["Name"] = item.Name,
            ["Item"] = itemData,
            ["UserId"] = JellyfinId(userId),
            ["UserName"] = userName,
            ["Timestamp"] = DateTimeOffset.UtcNow,
            ["UtcTimestamp"] = DateTimeOffset.UtcNow,
        };
    }

    // Jellyfin's API writes ids without dashes. Plekt keys echo markers and sessions by
    // that form, so a dashed id would never match them.
    private static string JellyfinId(Guid id) => id.ToString("N", CultureInfo.InvariantCulture);

    private static void AddPlaybackFields(
        IDictionary<string, object?> payload,
        long? positionTicks,
        bool? playedToCompletion,
        string? device,
        string? deviceId,
        string? client,
        string? sessionId)
    {
        if (positionTicks.HasValue)
        {
            payload["PlaybackPositionTicks"] = positionTicks.Value;
            payload["PlaybackPosition"] = TimeSpan.FromTicks(positionTicks.Value).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        }
        if (playedToCompletion.HasValue) payload["PlayedToCompletion"] = playedToCompletion.Value;
        if (!string.IsNullOrWhiteSpace(device)) payload["DeviceName"] = device;
        if (!string.IsNullOrWhiteSpace(deviceId)) payload["DeviceId"] = deviceId;
        if (!string.IsNullOrWhiteSpace(client)) payload["ClientName"] = client;
        if (!string.IsNullOrWhiteSpace(sessionId)) payload["SessionId"] = sessionId;
    }

    private async Task<bool> PostAsync(Dictionary<string, object?> payload, ILogger logger)
    {
        var configuration = Plugin.Instance?.Configuration;
        var webhookUrl = configuration?.WebhookUrl?.Trim();
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            // Keep existing installations working until they replace the split settings.
            var baseUrl = configuration?.PlektUrl?.Trim().TrimEnd('/');
            var secret = configuration?.WebhookSecret?.Trim();
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret)) return false;
            webhookUrl = $"{baseUrl}/api/webhook?token={Uri.EscapeDataString(secret)}";
        }

        if (!Uri.TryCreate(webhookUrl, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            logger.LogWarning("Plekt webhook delivery failed (invalid webhook address)");
            return false;
        }
        try
        {
            // Use Jellyfin's default named HTTP client.
            var client = _httpClientFactory.CreateClient(NamedClient.Default);
            using var timeout = new CancellationTokenSource(RequestTimeout);
            using var response = await client.PostAsJsonAsync(endpoint, payload, JsonOptions, timeout.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            // Do not log exception details: transport diagnostics can include request URLs.
            logger.LogWarning("Plekt webhook delivery failed ({Failure})", ex is HttpRequestException requestException
                ? requestException.StatusCode?.ToString() ?? "network error"
                : ex is TaskCanceledException ? "timeout" : "unexpected error");
            return false;
        }
    }
}
