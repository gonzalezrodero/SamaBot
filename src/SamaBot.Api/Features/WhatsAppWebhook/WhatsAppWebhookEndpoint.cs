using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SamaBot.Api.Common.Configuration;
using Wolverine;
using Wolverine.Http;

namespace SamaBot.Api.Features.WhatsAppWebhook;

public class WhatsAppWebhookEndpoint
{
[WolverineGet("/api/whatsapp/webhook")]
    public IResult VerifyChallenge(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge,
        IOptions<WhatsAppOptions> options,
        ILogger<WhatsAppWebhookEndpoint> logger)
    {
        var verifyToken = options.Value.VerifyToken;

        if (mode == "subscribe" && token == verifyToken && !string.IsNullOrEmpty(challenge))
        {
            return Results.Content(challenge, "text/plain");
        }

        logger.LogWarning("Webhook verification failed. Expected Token: '{Expected}', Received Token: '{Received}'", verifyToken, token);

        return Results.StatusCode(403);
    }

    [WolverinePost("/api/whatsapp/webhook")]
    public async Task<IResult> ReceiveMessage(
        HttpRequest request,
        IWhatsAppPayloadProcessor processor,
        IMessageBus bus)
    {
        /*if (!await processor.IsSignatureValidAsync(request))
        {
            return Results.Unauthorized();
        }*/

        var message = await processor.ExtractMessageAsync(request);
        if (message != null)
        {
            await bus.PublishAsync(message);
        }

        return Results.Ok();
    }
}