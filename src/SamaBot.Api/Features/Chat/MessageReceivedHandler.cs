using Marten;
using SamaBot.Api.Core.Events;
using SamaBot.Api.Features.Knowledge.Services;
using System.Text;
using Wolverine;

namespace SamaBot.Api.Features.Chat;

public static class MessageReceivedHandler
{
    public static async Task Handle(
        MessageReceived @event,
        IDocumentStore store,
        IKnowledgeBaseService knowledgeBase,
        IChatService chatService,
        IMessageBus bus,
        CancellationToken ct)
    {
        using var session = store.LightweightSession(@event.TenantId);

        var userText = @event.Text.Trim().ToUpperInvariant();
        if (BotPrompts.DeleteCommands.Contains(userText))
        {
            await SendDeleteCommandAsync(@event, session, bus, ct);
            return;
        }

        await ProcessResponseAsync(@event, session, knowledgeBase, chatService, bus, ct);
    }

    private static async Task SendDeleteCommandAsync(MessageReceived @event, IDocumentSession session, IMessageBus bus, CancellationToken ct)
    {
        var ackMessage = new ReplyGenerated(
            @event.MessageId,
            @event.BotPhoneNumberId,
            @event.PhoneNumber,
            BotPrompts.DeleteDataAutomaticReply,
            @event.TenantId);

        session.Events.Append(@event.PhoneNumber, ackMessage);
        await session.SaveChangesAsync(ct);
        await bus.InvokeAsync(ackMessage, ct);

        var command = new DeleteChatHistoryCommand(
            @event.PhoneNumber,
            @event.TenantId,
            @event.MessageId,
            @event.BotPhoneNumberId);

        await bus.InvokeAsync(command, ct);
    }

    private static async Task ProcessResponseAsync(
        MessageReceived @event,
        IDocumentSession session,
        IKnowledgeBaseService knowledgeBase,
        IChatService chatService,
        IMessageBus bus,
        CancellationToken ct)
    {
        var chatHistory = await ExtractChatHistory(@event.PhoneNumber, session, ct);

        var relevantChunks = await knowledgeBase.SearchAsync(@event.TenantId, @event.Text, limit: 10, ct);

        var contextBuilder = new StringBuilder();
        foreach (var chunk in relevantChunks)
        {
            contextBuilder.AppendLine(chunk.Content);
        }

        var privacyWarningRule = chatHistory.Count == 0 ? BotPrompts.PrivacyPolicyRule : string.Empty;
        var systemMessage = string.Format(BotPrompts.SystemPromptTemplate, privacyWarningRule, contextBuilder);
        var replyText = await chatService.GetResponseAsync(systemMessage, chatHistory, ct);

        if (string.IsNullOrWhiteSpace(replyText))
        {
            replyText = "I'm sorry, I couldn't process that request.";
        }

        var replyEvent = new ReplyGenerated(
            @event.MessageId,
            @event.BotPhoneNumberId,
            @event.PhoneNumber,
            replyText,
            @event.TenantId);

        session.Events.Append(@event.PhoneNumber, replyEvent);
        await session.SaveChangesAsync(ct);
        await bus.InvokeAsync(replyEvent, ct);
    }

    private static async Task<List<ChatMessage>> ExtractChatHistory(string phoneNumber, IDocumentSession session, CancellationToken ct)
    {
        var streamEvents = await session.Events.FetchStreamAsync(phoneNumber, token: ct);

        return [.. streamEvents.Select(evt => evt.Data switch
        {
            MessageReceived userMsg => new ChatMessage("user", userMsg.Text),
            ReplyGenerated botReply => new ChatMessage("assistant", botReply.Text),
            _ => null
        }).OfType<ChatMessage>()];
    }
}