using Marten;
using Marten.Events.Projections;
using SamaBot.Api.Core.Events;

namespace SamaBot.Api.Features.WhatsAppWebhook;

public class ProcessedMessageProjection : EventProjection
{
    public void Project(MessageReceived @event, IDocumentOperations ops)
    {
        // Store the ProcessedMessage document to ensure global idempotency.
        ops.Store(new ProcessedMessage
        {
            Id = @event.MessageId,
            ProcessedAt = @event.ReceivedAt
        });
    }
}