namespace SamaBot.Api.Core.Events;

public record MessageReceived(
    string MessageId,
    string BotPhoneNumberId,
    string PhoneNumber,
    string Text,
    DateTimeOffset ReceivedAt);
