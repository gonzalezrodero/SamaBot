using Microsoft.Extensions.Options;
using SamaBot.Api.Common.Configuration;
using SamaBot.Api.Core.Events;

namespace SamaBot.Api.Features.WhatsAppDispatcher;

public class ReplyGeneratedHandler
{
    private readonly IWhatsAppClient whatsappClient;
    private readonly WhatsAppOptions options;

    public ReplyGeneratedHandler(
        IWhatsAppClient whatsappClient,
        IOptions<WhatsAppOptions> options)
    {
        this.whatsappClient = whatsappClient;
        this.options = options.Value;

        ArgumentException.ThrowIfNullOrWhiteSpace(this.options.AccessToken);
    }

    public async Task Handle(ReplyGenerated @event, CancellationToken ct)
    {
        var token = $"Bearer {options.AccessToken}";

        var request = new WhatsAppTextRequest(
            To: @event.PhoneNumber,
            Text: new WhatsAppMessageBody(@event.Text)
        );

        await whatsappClient.SendMessageAsync(@event.BotPhoneNumberId, request, token, ct);
    }
}