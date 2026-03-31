using Microsoft.Extensions.AI;

namespace SamaBot.Api.Features.LanguageDetection;

public interface ILanguageDetector
{
    Task<string> DetectLanguageAsync(string text, CancellationToken cancellationToken = default);
}

public class LanguageDetector(IChatClient chatClient) : ILanguageDetector
{
    private static readonly ChatMessage SystemMessage = new(ChatRole.System,
        """
        You are a highly efficient language detection module for Club Bàsquet Samà.
        Rules:
        1. Analyze the user's text and output ONLY the strictly lowercase ISO 639-1 language code.
        2. Valid outputs: 'ca' (Catalan), 'es' (Spanish), 'en' (English).
        3. If you cannot determine the language reliably, or it's a mix, default to 'ca'.
        4. Do NOT output any conversational text, explanations, or punctuation. ONLY the code.
        """);

    public async Task<string> DetectLanguageAsync(string text, CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>(2)
        {
            SystemMessage,
            new(ChatRole.User, text)
        };

        var response = await chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
        var returnedCode = response.Text?.Trim().ToLowerInvariant() ?? "ca";

        return returnedCode is "es" or "en" ? returnedCode : "ca";
    }
}