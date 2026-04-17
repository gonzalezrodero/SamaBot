using SamaBot.Api.Features.Chat; // Asegúrate de tener el using de tu IChatService

namespace SamaBot.Api.Features.LanguageDetection;

public interface ILanguageDetector
{
    Task<string> DetectLanguageAsync(string text, CancellationToken cancellationToken = default);
}

public class LanguageDetector(IChatService chatService) : ILanguageDetector // 🚀 Inyectamos nuestro servicio
{
    private const string SystemPrompt = """
        You are a highly efficient language detection module for Club Bàsquet Samà.
        Rules:
        1. Analyze the user's text and output ONLY the strictly lowercase ISO 639-1 language code.
        2. Valid outputs: 'ca' (Catalan), 'es' (Spanish), 'en' (English).
        3. If you cannot determine the language reliably, or it's a mix, default to 'ca'.
        4. Do NOT output any conversational text, explanations, or punctuation. ONLY the code.
        """;

    public async Task<string> DetectLanguageAsync(string text, CancellationToken cancellationToken = default)
    {
        var responseText = await chatService.GetResponseAsync(SystemPrompt, text, cancellationToken);

        var returnedCode = responseText?.Trim().ToLowerInvariant() ?? "ca";

        return returnedCode is "es" or "en" ? returnedCode : "ca";
    }
}