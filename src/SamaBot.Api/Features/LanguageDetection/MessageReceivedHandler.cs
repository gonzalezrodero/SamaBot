using Marten;
using SamaBot.Api.Core.Events;

namespace SamaBot.Api.Features.LanguageDetection;

public class MessageReceivedHandler
{
    public static async Task<MessageAnalyzed> Handle(
        MessageReceived @event,
        IDocumentStore store,
        ILanguageDetector languageDetector,
        CancellationToken cancellationToken)
    {
        var languageCode = await languageDetector.DetectLanguageAsync(@event.Text, cancellationToken);

        var analyzedEvent = new MessageAnalyzed(
            MessageId: @event.MessageId,
            BotPhoneNumberId: @event.BotPhoneNumberId,
            PhoneNumber: @event.PhoneNumber,
            LanguageCode: languageCode,
            OriginalText: @event.Text
        );

        using var session = store.LightweightSession(@event.BotPhoneNumberId);
        session.Events.Append(@event.PhoneNumber, analyzedEvent);

        await session.SaveChangesAsync(cancellationToken);

        return analyzedEvent;
    }
}