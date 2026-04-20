using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace SamaBot.Api.Features.Chat;

public interface IChatService
{
    Task<string> GetResponseAsync(string systemPrompt, string userPrompt, CancellationToken ct);
}

public class ChatService(IAmazonBedrockRuntime client, IOptions<BedrockSettings> settings, ILogger<ChatService> logger) : IChatService
{
    private readonly BedrockSettings settings = settings.Value;

    public async Task<string> GetResponseAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        logger.LogDebug("GetResponseAsync: calling Bedrock ModelId={ModelId} MaxTokens={MaxTokens}", settings.ModelId, settings.MaxTokens);

        var payload = new
        {
            anthropic_version = "bedrock-2023-05-31",
            max_tokens = settings.MaxTokens,
            temperature = settings.Temperature,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = userPrompt }
            }
        };

        var request = new InvokeModelRequest
        {
            ModelId = settings.ModelId,
            ContentType = "application/json",
            Accept = "application/json",
            Body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(payload))
        };

        try
        {
            var response = await client.InvokeModelAsync(request, ct);

            using var reader = new StreamReader(response.Body);
            var responseBody = await reader.ReadToEndAsync(ct);

            logger.LogDebug("Bedrock response length: {Length}", responseBody?.Length ?? 0);

            var result = JsonDocument.Parse(responseBody);
            var text = result.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? string.Empty;

            logger.LogInformation("ChatService received response of length {Len}", text.Length);
            return text;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling Bedrock InvokeModel for ModelId={ModelId}", settings.ModelId);
            throw;
        }
    }
}