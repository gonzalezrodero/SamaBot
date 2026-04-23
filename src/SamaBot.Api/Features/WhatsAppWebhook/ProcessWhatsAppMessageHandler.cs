using Marten;
using SamaBot.Api.Core.Events;

namespace SamaBot.Api.Features.WhatsAppWebhook;

public class ProcessWhatsAppMessageHandler
{
    public async Task<MessageReceived?> Handle(
        ProcessWhatsAppMessage command,
        IDocumentStore store, 
        CancellationToken ct)
    {
        using var session = store.LightweightSession(command.BotPhoneNumberId);

        if (await session.Query<ProcessedMessage>().AnyAsync(x => x.Id == command.MessageId, ct))
        {
            return null;
        }

        var receivedEvent = new MessageReceived(
            MessageId: command.MessageId,
            BotPhoneNumberId: command.BotPhoneNumberId,
            PhoneNumber: command.PhoneNumber,
            Text: command.Text,
            ReceivedAt: command.Timestamp
        );

        session.Events.Append(command.PhoneNumber, receivedEvent);
        await session.SaveChangesAsync(ct);

        return receivedEvent;
    }
}