namespace SamaBot.Api.Core.Events;

/// <summary>
/// Represents a message received from a parent via the WhatsApp Webhook.
/// </summary>
public record MessageReceived(
    string MessageId,
    string BotPhoneNumberId,
    string PhoneNumber,
    string Text,
    DateTimeOffset ReceivedAt);
