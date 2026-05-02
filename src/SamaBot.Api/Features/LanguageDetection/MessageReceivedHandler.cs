using Marten;
using SamaBot.Api.Core.Events;
using Wolverine;

namespace SamaBot.Api.Features.LanguageDetection;

public static class MessageReceivedHandler
{
    public static async Task Handle(
        MessageReceived @event,
        IDocumentStore store,
        ILanguageDetector languageDetector,
        IMessageBus bus,
        CancellationToken ct)
    {
        var languageCode = await languageDetector.DetectLanguageAsync(@event.Text, ct);

        var analyzedEvent = new MessageAnalyzed(
            MessageId: @event.MessageId,
            BotPhoneNumberId: @event.BotPhoneNumberId,
            PhoneNumber: @event.PhoneNumber,
            LanguageCode: languageCode,
            OriginalText: @event.Text,
            TenantId: @event.TenantId
        );

        using var session = store.LightweightSession(@event.TenantId);

        session.Events.Append(@event.PhoneNumber, analyzedEvent);
        await session.SaveChangesAsync(ct);

        await bus.InvokeAsync(analyzedEvent, ct);
    }
}