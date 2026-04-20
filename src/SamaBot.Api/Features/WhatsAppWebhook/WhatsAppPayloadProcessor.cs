using Microsoft.Extensions.Options;
using SamaBot.Api.Common.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SamaBot.Api.Features.WhatsAppWebhook;

public interface IWhatsAppPayloadProcessor
{
    Task<bool> IsSignatureValidAsync(HttpRequest request);
    Task<ProcessWhatsAppMessage?> ExtractMessageAsync(HttpRequest request);
}

public class WhatsAppPayloadProcessor : IWhatsAppPayloadProcessor
{
    private readonly WhatsAppOptions options;
    private readonly ILogger<WhatsAppPayloadProcessor> logger;

    public WhatsAppPayloadProcessor(IOptions<WhatsAppOptions> options, ILogger<WhatsAppPayloadProcessor> logger)
    {
        this.options = options.Value;
        this.logger = logger;

        ArgumentException.ThrowIfNullOrWhiteSpace(this.options.AppSecret);
        logger.LogDebug("WhatsAppPayloadProcessor initialized. AppSecret present: {HasSecret}", !string.IsNullOrEmpty(this.options.AppSecret));
    }

    public async Task<bool> IsSignatureValidAsync(HttpRequest request)
    {
        var signatureHeader = request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith("sha256="))
        {
            logger.LogInformation("Signature header missing or malformed.");
            return false;
        }

        var incomingSignature = signatureHeader["sha256=".Length..];
        logger.LogDebug("Incoming signature prefix: {Prefix}", incomingSignature.Length > 8 ? incomingSignature[..8] : incomingSignature);

        var body = await ReadRequestBodyAsync(request);
        if (body == null)
        {
            logger.LogWarning("Request body could not be read for signature validation.");
            return false;
        }

        var expectedSignature = ComputeSignature(body);
        logger.LogDebug("Computed signature prefix: {Prefix}", expectedSignature.Length > 8 ? expectedSignature[..8] : expectedSignature);

        var valid = incomingSignature.Equals(expectedSignature, StringComparison.OrdinalIgnoreCase);
        logger.LogInformation("Signature validation {Result}", valid ? "succeeded" : "failed");

        return valid;
    }

    public async Task<ProcessWhatsAppMessage?> ExtractMessageAsync(HttpRequest request)
    {
        var body = await ReadRequestBodyAsync(request);
        logger.LogDebug("ExtractMessageAsync: body length {Length}", body?.Length ?? 0);

        var message = ParseJsonToMessage(body);
        if (message != null)
        {
            logger.LogInformation("Parsed message: Id={MessageId} From={From} BotNumberId={BotNumberId} TextLength={TextLength}",
                message.MessageId, message.PhoneNumber, message.BotPhoneNumberId, message.Text?.Length ?? 0);
        }
        else
        {
            logger.LogInformation("No parsable WhatsApp message found in payload.");
        }

        return message;
    }

    private async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        try
        {
            request.EnableBuffering();
            request.Body.Position = 0;
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            logger.LogDebug("ReadRequestBodyAsync: read {Bytes} bytes", Encoding.UTF8.GetByteCount(body));
            return body;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read request body.");
            return string.Empty;
        }
    }

    private string ComputeSignature(string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.AppSecret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hashBytes);
    }

    private ProcessWhatsAppMessage? ParseJsonToMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (!root.TryGetProperty("entry", out var entries) || entries.GetArrayLength() == 0) return null;

            var firstEntry = entries[0];
            if (!firstEntry.TryGetProperty("changes", out var changes) || changes.GetArrayLength() == 0) return null;

            var valueNode = changes[0].GetProperty("value");
            if (!valueNode.TryGetProperty("messages", out var messages) || messages.GetArrayLength() == 0) return null;

            var messageNode = messages[0];

            var fromNumber = messageNode.GetProperty("from").GetString();
            var messageId = messageNode.GetProperty("id").GetString();
            var timestampStr = messageNode.GetProperty("timestamp").GetString();
            var botNumberId = valueNode.GetProperty("metadata").GetProperty("phone_number_id").GetString();

            if (!messageNode.TryGetProperty("text", out var textNode) || !textNode.TryGetProperty("body", out var bodyNode)) return null;

            var messageText = bodyNode.GetString();

            if (string.IsNullOrEmpty(fromNumber) || string.IsNullOrEmpty(messageText) || string.IsNullOrEmpty(messageId) || timestampStr == null || botNumberId == null)
            {
                return null;
            }

            var timestamp = DateTimeOffset.FromUnixTimeSeconds(long.Parse(timestampStr));
            return new ProcessWhatsAppMessage(messageId, botNumberId, fromNumber, messageText, timestamp, body);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse JSON payload as WhatsApp message.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unexpected error parsing WhatsApp payload.");
        }

        return null; // Not a standard incoming text message
    }
}