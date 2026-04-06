namespace SamaBot.Api.Features.WhatsAppWebhook;

public class ProcessedMessage
{
    public string Id { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}