using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SamaBot.Api.Features.WhatsAppWebhook;

public interface IWhatsAppPayloadProcessor
{
    Task<bool> IsSignatureValidAsync(HttpRequest request);
    Task<ProcessWhatsAppMessage?> ExtractMessageAsync(HttpRequest request);
}

public class WhatsAppPayloadProcessor(IConfiguration config) : IWhatsAppPayloadProcessor
{
    private readonly string _secret = config["WhatsApp:App_Secret"] 
        ?? throw new InvalidOperationException("WhatsApp:App_Secret is not configured.");

    public async Task<bool> IsSignatureValidAsync(HttpRequest request)
    {
        var signatureHeader = request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith("sha256="))
        {
            return false;
        }

        var incomingSignature = signatureHeader["sha256=".Length..];
        
        var body = await ReadRequestBodyAsync(request);
        var expectedSignature = ComputeSignature(body);

        return incomingSignature == expectedSignature;
    }

    public async Task<ProcessWhatsAppMessage?> ExtractMessageAsync(HttpRequest request)
    {
        var body = await ReadRequestBodyAsync(request);
        return ParseJsonToMessage(body);
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0; // Reset for downstream parsers
        
        return body;
    }

    private string ComputeSignature(string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        
        return Convert.ToHexStringLower(hashBytes);
    }

    private static ProcessWhatsAppMessage? ParseJsonToMessage(string body)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        
        try 
        {
            var valueNode = json.GetProperty("entry")[0].GetProperty("changes")[0].GetProperty("value");
            var messageNode = valueNode.GetProperty("messages")[0];
            
            var fromNumber = messageNode.GetProperty("from").GetString();
            var messageId = messageNode.GetProperty("id").GetString();
            var messageText = messageNode.GetProperty("text").GetProperty("body").GetString();
            
            var timestampStr = messageNode.GetProperty("timestamp").GetString();
            var botNumberId = valueNode.GetProperty("metadata").GetProperty("phone_number_id").GetString();

            if (!string.IsNullOrEmpty(fromNumber) && !string.IsNullOrEmpty(messageText) && !string.IsNullOrEmpty(messageId) && timestampStr != null && botNumberId != null)
            {
                var timestamp = DateTimeOffset.FromUnixTimeSeconds(long.Parse(timestampStr));
                return new ProcessWhatsAppMessage(messageId, botNumberId, fromNumber, messageText, timestamp, body);
            }
        } 
        catch (Exception ex) when (ex is KeyNotFoundException || ex is IndexOutOfRangeException || ex is InvalidOperationException)
        {
            // Safely ignored: this is not a standard incoming text message (e.g., status, image, audio)
        }

        return null;
    }
}
