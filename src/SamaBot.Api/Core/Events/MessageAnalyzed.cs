namespace SamaBot.Api.Core.Events;

public record MessageAnalyzed(
    string MessageId,
    string BotPhoneNumberId,
    string PhoneNumber,
    string LanguageCode,
    string OriginalText);