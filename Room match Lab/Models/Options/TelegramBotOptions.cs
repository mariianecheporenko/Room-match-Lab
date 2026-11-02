namespace RoomMates.Models.Options;

public sealed class TelegramBotOptions
{
    public const string SectionName = "TelegramBot";

    public string? BotToken { get; set; }
    public string? ChatId { get; set; }
    public bool IsEnabled { get; set; }
}
