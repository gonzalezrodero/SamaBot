using Microsoft.AspNetCore.Mvc;
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
        IConfiguration config)
    {
        var verifyToken = config["WhatsApp:Verify_Token"] ?? "samabot_token";
        
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
        IMessageBus bus)
    {
        request.EnableBuffering(); 
        if (!await processor.IsSignatureValidAsync(request))
        {
            return Results.Unauthorized();
        }

        var message = await processor.ExtractMessageAsync(request);
        if (message != null)
        {
            await bus.InvokeAsync(message);
        }

        return Results.Ok();
    }
}
