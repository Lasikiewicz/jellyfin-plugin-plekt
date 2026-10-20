using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Plekt;

/// <summary>
/// Creates Jellyfin Webhook plugin-compatible payloads and posts them to Plekt.
/// </summary>
internal static class PlektWebhookSender
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = null };

    public static async Task SendPlaybackAsync(
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
            var payload = CreatePayload(notificationType, item, user.Id, user.Username);
            AddPlaybackFields(payload, positionTicks, playedToCompletion, device, deviceId, client, sessionId);
            await PostAsync(payload, logger).ConfigureAwait(false);
        }
    }

    public static async Task SendUserDataAsync(UserDataSaveEventArgs eventArgs, ILogger logger)
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

        await PostAsync(payload, logger).ConfigureAwait(false);
    }

    private static Dictionary<string, object?> CreatePayload(string notificationType, BaseItem item, Guid userId, string userName)
    {
        var itemData = new Dictionary<string, object?>
        {
            ["Id"] = item.Id.ToString(),
            ["Type"] = item.GetType().Name,
            ["Name"] = item.Name,
            ["ProviderIds"] = item.ProviderIds,
            ["RunTimeTicks"] = item.RunTimeTicks,
            ["ProductionYear"] = item.ProductionYear,
            ["PremiereDate"] = item.PremiereDate,
            ["ParentId"] = item.ParentId.ToString(),
            ["ParentIndexNumber"] = item.ParentIndexNumber,
            ["IndexNumber"] = item.IndexNumber,
        };

        if (item is Episode episode)
        {
            itemData["SeriesId"] = episode.SeriesId.ToString();
            itemData["SeriesName"] = episode.Series?.Name;
            itemData["SeriesProviderIds"] = episode.Series?.ProviderIds;
            itemData["SeriesProductionYear"] = episode.Series?.ProductionYear;
        }

        return new Dictionary<string, object?>
        {
            ["NotificationType"] = notificationType,
            ["ItemId"] = item.Id.ToString(),
            ["ItemType"] = item.GetType().Name,
            ["Name"] = item.Name,
            ["Item"] = itemData,
            ["UserId"] = userId.ToString(),
            ["UserName"] = userName,
            ["Timestamp"] = DateTimeOffset.UtcNow,
            ["UtcTimestamp"] = DateTimeOffset.UtcNow,
        };
    }

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

    private static async Task PostAsync(Dictionary<string, object?> payload, ILogger logger)
    {
        var configuration = Plugin.Instance?.Configuration;
        var baseUrl = configuration?.PlektUrl?.Trim().TrimEnd('/');
        var secret = configuration?.WebhookSecret?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret)) return;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)) return;

        var endpoint = new Uri($"{baseUrl}/api/webhook?token={WebUtility.UrlEncode(secret)}");
        try
        {
            using var response = await Client.PostAsJsonAsync(endpoint, payload, JsonOptions).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            // Do not log exception details: transport diagnostics can include request URLs.
            logger.LogWarning("Plekt webhook delivery failed ({Failure})", ex is HttpRequestException requestException
                ? requestException.StatusCode?.ToString() ?? "network error"
                : ex is TaskCanceledException ? "timeout" : "unexpected error");
        }
    }
}
