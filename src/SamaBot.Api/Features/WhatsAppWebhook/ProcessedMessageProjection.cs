using Marten;
using Marten.Events.Projections;
using SamaBot.Api.Core.Events;

namespace SamaBot.Api.Features.WhatsAppWebhook;

public class ProcessedMessageProjection : EventProjection
{
    public void Project(MessageReceived @event, IDocumentOperations ops)
    {
        ops.Store(new ProcessedMessage
        {
            Id = @event.MessageId,
            ProcessedAt = @event.ReceivedAt,
            TenantId = @event.TenantId,
            BotPhoneNumberId = @event.BotPhoneNumberId,
            PhoneNumber = @event.PhoneNumber
        });
    }
}