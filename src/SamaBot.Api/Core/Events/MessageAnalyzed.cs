namespace SamaBot.Api.Core.Events;

public record MessageAnalyzed(
    string MessageId,
    string PhoneNumber,
    string LanguageCode,
    string OriginalText);
