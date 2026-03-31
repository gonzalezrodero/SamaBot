using Marten;
using Microsoft.Extensions.AI;
using SamaBot.Api.Core.Events;
using SamaBot.Api.Features.Knowledge;
using System.Text;

namespace SamaBot.Api.Features.Chat;

public class MessageAnalyzedHandler(
    IKnowledgeBaseService knowledgeBase,
    IChatClient chatClient)
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

        // 2. AI Logic
        var systemMessage = string.Format(SystemPromptTemplate, @event.LanguageCode, contextBuilder);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemMessage),
            new(ChatRole.User, @event.OriginalText)
        };

        var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
        var replyText = response.Text ?? "I'm sorry, I couldn't process that request.";

        // 3. Persistence & Event Sourcing
        var replyEvent = new ReplyGenerated(@event.MessageId, @event.PhoneNumber, replyText);
        session.Events.Append(@event.PhoneNumber, replyEvent);

        return replyEvent;
    }
}