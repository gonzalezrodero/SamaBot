using Marten;
using SamaBot.Api.Core.Events;

namespace SamaBot.Api.Features.WhatsAppWebhook;

public static class ProcessWhatsAppMessageHandler
{
    public static async Task<MessageReceived?> Handle(ProcessWhatsAppMessage command, IDocumentSession session)
    {
        // 1. Idempotency Check (Meta Retry Problem)
        if (await session.Query<ProcessedMessage>().AnyAsync(x => x.Id == command.MessageId))
        {
            return null; 
        }

        var received = new MessageReceived(
            MessageId: command.MessageId,
            BotPhoneNumberId: command.BotPhoneNumberId,
            PhoneNumber: command.PhoneNumber,
            Text: command.Text,
            ReceivedAt: command.Timestamp
        );

        session.Events.Append(command.PhoneNumber, received);
        return received;
    }
}
