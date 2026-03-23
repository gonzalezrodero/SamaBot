using Marten;
using SamaBot.Api.Core.Events;

namespace SamaBot.Api.Features.WhatsAppWebhook;

public record ProcessWhatsAppMessage(string MessageId, string BotPhoneNumberId, string PhoneNumber, string Text, DateTimeOffset Timestamp, string RawPayload);

public class ProcessWhatsAppMessageHandler
{
    public async Task<MessageReceived?> Handle(ProcessWhatsAppMessage command, IDocumentSession session)
    {
        // 1. Idempotency Check (Meta Retry Problem)
        // Meta will retry webhooks aggressively if there's latency. We fetch the stream 
        // for this sender to ensure we haven't already captured this exact MessageId.
        var existingEvents = await session.Events.FetchStreamAsync(command.PhoneNumber);
        if (existingEvents.Any(e => e.Data is MessageReceived mr && mr.MessageId == command.MessageId))
        {
            // Silently ignore the duplicate webhook
            return null;
        }

        // For the MVP, we assume language detection starts as default ("ca" - Catalan)
        var messageReceived = new MessageReceived(
            MessageId: command.MessageId,
            BotPhoneNumberId: command.BotPhoneNumberId,
            PhoneNumber: command.PhoneNumber,
            Text: command.Text,
            ReceivedAt: command.Timestamp
        );

        // Append to Marten stream using the sender's phone number as the StreamId
        session.Events.Append(command.PhoneNumber, messageReceived);
        
        // Return the event to cascade processing to the next handler in the pipeline
        return messageReceived;
    }
}
