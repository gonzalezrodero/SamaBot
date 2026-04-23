using Marten;
using SamaBot.Api.Core.Events;
using SamaBot.Api.Features.Tenancy;

namespace SamaBot.Api.Features.WhatsAppWebhook;

public static class ProcessWhatsAppMessageHandler
{
    public static async Task<MessageReceived?> Handle(
        ProcessWhatsAppMessage command,
        IDocumentStore store,
        CancellationToken ct)
    {
        // 1. Global search (non-tenant) to find who owns this phone ID
        using var querySession = store.QuerySession();
        var tenant = await querySession.Query<TenantProfile>()
            .FirstOrDefaultAsync(x => x.BotPhoneNumberId == command.BotPhoneNumberId, ct);

        if (tenant == null)
        {
            return null;
        }

        using var session = store.LightweightSession(tenant.Id);
        if (await session.Query<ProcessedMessage>().AnyAsync(x => x.Id == command.MessageId, ct))
        {
            return null;
        }

        var receivedEvent = new MessageReceived(
            MessageId: command.MessageId,
            PhoneNumber: command.PhoneNumber,
            Text: command.Text,
            TenantId: tenant.Id,
            BotPhoneNumberId: command.BotPhoneNumberId,
            ReceivedAt: command.Timestamp
        );

        session.Events.Append(command.PhoneNumber, receivedEvent);
        await session.SaveChangesAsync(ct);

        return receivedEvent;
    }
}