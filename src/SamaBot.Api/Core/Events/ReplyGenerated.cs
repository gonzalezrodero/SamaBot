namespace SamaBot.Api.Core.Events;

public record ReplyGenerated(string MessageId, string PhoneNumber, string Text);