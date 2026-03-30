using Marten;
using SamaBot.Api.Core.Events;

namespace SamaBot.Api.Features.LanguageDetection;

public class MessageReceivedHandler(ILanguageDetector languageDetector)
{
    public async Task<MessageAnalyzed> Handle(MessageReceived @event, IDocumentSession session, CancellationToken cancellationToken)
    {
        // Use the LLM abstraction to classify the language
        var languageCode = await languageDetector.DetectLanguageAsync(@event.Text, cancellationToken);
        
        var analyzedEvent = new MessageAnalyzed(
            MessageId: @event.MessageId,
            PhoneNumber: @event.PhoneNumber,
            LanguageCode: languageCode,
            @event.Text
        );

        // Append the new phase to the Marten event stream
        session.Events.Append(@event.PhoneNumber, analyzedEvent);

        return analyzedEvent;
    }
}
