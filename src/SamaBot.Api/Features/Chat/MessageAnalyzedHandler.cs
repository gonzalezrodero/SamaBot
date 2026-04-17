using Marten;
using SamaBot.Api.Core.Events;
using SamaBot.Api.Features.Knowledge;
using System.Text;

namespace SamaBot.Api.Features.Chat;

public class MessageAnalyzedHandler(
    IKnowledgeBaseService knowledgeBase,
    IChatService chatService) // 👈 Cambiamos IChatClient por tu nuevo servicio
{
    private const string SystemPromptTemplate = """
        You are a helpful assistant on WhatsApp.
        Answer the user's question using ONLY the provided context.
        If the answer is not in the context, say you don't know.
        Reply in this language code: {0}.

        Context:
        {1}
        """;

    public async Task<ReplyGenerated> Handle(
        MessageAnalyzed @event,
        IDocumentSession session,
        CancellationToken ct)
    {
        // 1. RAG - Retrieval
        var relevantChunks = await knowledgeBase.SearchAsync(@event.OriginalText, limit: 3, ct: ct);

        var contextBuilder = new StringBuilder();
        foreach (var chunk in relevantChunks)
        {
            contextBuilder.AppendLine(chunk.Content);
        }

        // 2. AI Logic con Bedrock
        var systemMessage = string.Format(SystemPromptTemplate, @event.LanguageCode, contextBuilder);

        // Llamamos a Bedrock pasando el System y el User prompt por separado
        var replyText = await chatService.GetResponseAsync(systemMessage, @event.OriginalText, ct);

        if (string.IsNullOrWhiteSpace(replyText))
            replyText = "I'm sorry, I couldn't process that request.";

        // 3. Persistence & Event Sourcing (Marten)
        var replyEvent = new ReplyGenerated(@event.MessageId, @event.PhoneNumber, replyText);
        session.Events.Append(@event.PhoneNumber, replyEvent);

        return replyEvent;
    }
}