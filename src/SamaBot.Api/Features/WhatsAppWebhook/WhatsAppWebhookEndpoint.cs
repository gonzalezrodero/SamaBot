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
            IMessageBus bus,
            ILogger<WhatsAppWebhookEndpoint> logger)
    {
        // Forzamos la lectura del body aquí mismo para verlo en CloudWatch
        request.EnableBuffering();
        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        logger.LogWarning(">>> [DEBUG] RAW BODY RECIBIDO: {Body}", rawBody);

        var message = await processor.ExtractMessageAsync(request);

        if (message != null)
        {
            logger.LogWarning(">>> [DEBUG] Message extracted successfully.");
            await bus.PublishAsync(message);
            logger.LogWarning(">>> [DEBUG] Message successfully published to SQS bus.");
        }
        else
        {
            logger.LogWarning(">>> [DEBUG] Extraction returned NULL. El parseo falló.");
        }

        return Results.Ok();
    }
}