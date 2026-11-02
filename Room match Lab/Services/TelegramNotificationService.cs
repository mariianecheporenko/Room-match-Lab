using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Net;
using Microsoft.Extensions.Options;
using RoomMates.Models;
using RoomMates.Models.Options;

namespace RoomMates.Services;

public sealed class TelegramNotificationService(
    HttpClient httpClient,
    IOptions<TelegramBotOptions> options,
    ILogger<TelegramNotificationService> logger) : ITelegramNotificationService
{
    public async Task<bool> SendBookingNotificationAsync(
        BookingRequest bookingRequest,
        string studentName,
        string housingTitle,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!CanSend(settings))
        {
            LogUnavailable(settings);
            return false;
        }

        var payload = new TelegramMessage(
            settings.ChatId!,
            $"<b>Нова заявка на поселення!</b>\n\n<b>Студент:</b> {WebUtility.HtmlEncode(studentName)}\n<b>Житло:</b> {WebUtility.HtmlEncode(housingTitle)}\n<b>Сумісність:</b> {bookingRequest.MatchScore.ToString("F2", CultureInfo.InvariantCulture)}%\n<b>Статус:</b> Pending",
            "HTML");

        return await SendMessageAsync(settings, payload, "booking", cancellationToken);
    }

    public async Task<bool> SendLandlordApplicationNotificationAsync(
        Profile profile,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!CanSend(settings))
        {
            LogUnavailable(settings);
            return false;
        }

        var payload = new TelegramMessage(
            settings.ChatId!,
            $"<b>Нова заявка на статус власника житла</b>\n\n<b>Користувач:</b> {WebUtility.HtmlEncode(profile.FullName)}\n<b>Email:</b> {WebUtility.HtmlEncode(profile.Email)}\n<b>Статус:</b> Очікує на апрув адміністратора",
            "HTML");
        return await SendMessageAsync(settings, payload, "landlord application", cancellationToken);
    }

    private async Task<bool> SendMessageAsync(
        TelegramBotOptions settings,
        TelegramMessage payload,
        string notificationType,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                $"https://api.telegram.org/bot{settings.BotToken}/sendMessage",
                payload,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Telegram API returned HTTP status {StatusCode} for a {NotificationType} notification.", response.StatusCode, notificationType);
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to send a {NotificationType} notification through Telegram.", notificationType);
            return false;
        }
    }

    private bool CanSend(TelegramBotOptions settings) =>
        settings.IsEnabled &&
        !string.IsNullOrWhiteSpace(settings.BotToken) &&
        settings.BotToken != "YOUR_TELEGRAM_BOT_TOKEN" &&
        !string.IsNullOrWhiteSpace(settings.ChatId) &&
        settings.ChatId != "YOUR_TELEGRAM_CHAT_ID";

    private void LogUnavailable(TelegramBotOptions settings)
    {
        if (!settings.IsEnabled)
            logger.LogWarning("Telegram notifications are disabled; notification was skipped.");
        else
            logger.LogWarning("Telegram notifications are enabled but the bot token or chat ID is not configured.");
    }

    private sealed record TelegramMessage(
        [property: JsonPropertyName("chat_id")] string ChatId,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("parse_mode")] string ParseMode);
}
