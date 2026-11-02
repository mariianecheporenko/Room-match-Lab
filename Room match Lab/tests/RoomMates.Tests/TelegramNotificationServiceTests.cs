using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using RoomMates.Models;
using RoomMates.Models.Options;
using RoomMates.Services;

namespace RoomMates.Tests;

public class TelegramNotificationServiceTests
{
    [Fact]
    public async Task SendNotification_WhenDisabled_ReturnsFalseAndDoesNotSendHttp()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var (service, _, _, _) = CreateService(handler, new TelegramBotOptions
        {
            IsEnabled = false,
            BotToken = "token",
            ChatId = "123"
        });

        var result = await service.SendBookingNotificationAsync(new BookingRequest(), "Student", "Housing");

        result.Should().BeFalse();
        handler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendNotification_WhenSuccessful_SendsCorrectJsonPayload()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken _) =>
            {
                capturedRequest = request;
                capturedBody = await request.Content!.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
        var (service, _, factory, _) = CreateService(handler, EnabledOptions());

        var result = await service.SendBookingNotificationAsync(
            new BookingRequest { MatchScore = 87.5 }, "Ada Student", "Sunny Room");

        result.Should().BeTrue();
        factory.Verify(clientFactory => clientFactory.CreateClient(nameof(ITelegramNotificationService)), Times.Once);
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri!.ToString().Should().Be("https://api.telegram.org/bottoken/sendMessage");
        using var json = JsonDocument.Parse(capturedBody!);
        json.RootElement.GetProperty("chat_id").GetString().Should().Be("123456");
        json.RootElement.GetProperty("text").GetString().Should().Be(
            "<b>Нова заявка на поселення!</b>\n\n<b>Студент:</b> Ada Student\n<b>Житло:</b> Sunny Room\n<b>Сумісність:</b> 87.50%\n<b>Статус:</b> Pending");
        json.RootElement.GetProperty("parse_mode").GetString().Should().Be("HTML");
        json.RootElement.EnumerateObject().Should().HaveCount(3);
    }

    [Fact]
    public async Task SendNotification_WhenApiFails_LogsWarningAndReturnsFalse()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var (service, _, _, logger) = CreateService(handler, EnabledOptions());

        var result = await service.SendBookingNotificationAsync(new BookingRequest(), "Student", "Housing");

        result.Should().BeFalse();
        logger.VerifyLogWarning();
    }

    private static TelegramBotOptions EnabledOptions() => new()
    {
        IsEnabled = true,
        BotToken = "token",
        ChatId = "123456"
    };

    private static (TelegramNotificationService Service, Mock<HttpMessageHandler> Handler, Mock<IHttpClientFactory> Factory, Mock<ILogger<TelegramNotificationService>> Logger) CreateService(
        Mock<HttpMessageHandler> handler,
        TelegramBotOptions settings)
    {
        var client = new HttpClient(handler.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(clientFactory => clientFactory.CreateClient(nameof(ITelegramNotificationService)))
            .Returns(client);
        var typedClient = factory.Object.CreateClient(nameof(ITelegramNotificationService));
        var logger = new Mock<ILogger<TelegramNotificationService>>();
        return (new TelegramNotificationService(typedClient, Options.Create(settings), logger.Object), handler, factory, logger);
    }
}

internal static class TelegramTestLoggerExtensions
{
    public static void VerifyLogWarning(this Mock<ILogger<TelegramNotificationService>> logger)
    {
        logger.Verify(
            item => item.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => (state!).ToString()!.Contains("Telegram API returned HTTP status")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
