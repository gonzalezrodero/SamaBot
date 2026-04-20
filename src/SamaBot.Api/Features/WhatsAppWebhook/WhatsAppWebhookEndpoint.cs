using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SamaBot.Api.Common.Configuration;
using Wolverine;
using Wolverine.Http;

namespace SamaBot.Api.Features.WhatsAppWebhook;

public class WhatsAppWebhookEndpoint
{
    [WolverineGet("/api/whatsapp/webhook")]
    public string VerifyChallenge(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge,
        IOptions<WhatsAppOptions> options)
    {
        var verifyToken = options.Value.VerifyToken;

        if (mode == "subscribe" && token == verifyToken && challenge != null)
        {
            return challenge;
        }

        throw new BadHttpRequestException("Invalid verification token.", 403);
    }

    [WolverinePost("/api/whatsapp/webhook")]
    public async Task<IResult> ReceiveMessage(
        HttpRequest request,
        IWhatsAppPayloadProcessor processor,
        IMessageBus bus,
        ILogger<WhatsAppWebhookEndpoint> logger)
    {
        logger.LogInformation("Webhook received. RemoteIp={RemoteIp} Path={Path}", request.HttpContext.Connection.RemoteIpAddress, request.Path);

        var signatureHeader = request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        logger.LogDebug("Signature header present: {HasHeader}", !string.IsNullOrEmpty(signatureHeader));

        if (!await processor.IsSignatureValidAsync(request))
        {
            logger.LogWarning("Request unauthorized: invalid signature.");
            return Results.Unauthorized();
        }

        var message = await processor.ExtractMessageAsync(request);
        if (message != null)
        {
            logger.LogInformation("Dispatching message to bus. MessageId={MessageId}", message.MessageId);
            await bus.InvokeAsync(message);
            logger.LogInformation("Message dispatched. MessageId={MessageId}", message.MessageId);
        }
        else
        {
            logger.LogInformation("No message to dispatch after parsing.");
        }

        return Results.Ok();
    }
}