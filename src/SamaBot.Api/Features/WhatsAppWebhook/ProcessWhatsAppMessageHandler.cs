using Marten;
using SamaBot.Api.Core.Events;

namespace SamaBot.Api.Features.WhatsAppWebhook;

public record ProcessWhatsAppMessage(string MessageId, string BotPhoneNumberId, string PhoneNumber, string Text, DateTimeOffset Timestamp, string RawPayload);

public static class ProcessWhatsAppMessageHandler
{
    public static async Task<MessageReceived?> Handle(ProcessWhatsAppMessage command, IDocumentSession session)
    {
        // 1. Idempotency Check (Meta Retry Problem)
        var existingEvents = await session.Events.FetchStreamAsync(command.PhoneNumber);
        if (existingEvents.Any(e => e.Data is MessageReceived mr && mr.MessageId == command.MessageId))
        {
            return null;
        }

        var messageReceived = new MessageReceived(
            MessageId: command.MessageId,
            BotPhoneNumberId: command.BotPhoneNumberId,
            PhoneNumber: command.PhoneNumber,
            Text: command.Text,
            ReceivedAt: command.Timestamp
        );

        session.Events.Append(command.PhoneNumber, messageReceived);
        return messageReceived;
    }
}
