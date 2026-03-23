using SamaBot.Api.Features.WhatsAppWebhook;
using SamaBot.Api.Features.LanguageDetection;

namespace SamaBot.Api;

public static class Config
{
    /// <summary>
    /// Extension method to aggregate all vertical slice feature configurations.
    /// This prevents Program.cs from becoming bloated.
    /// </summary>
    public static IServiceCollection AddSamaBotFeatures(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddWhatsAppWebhookFeature(configuration);
        services.AddLanguageDetectionFeature(configuration);
        
        // Future phases (Localization, RAG, AI Generation, Dispatch) will be registered here
        
        return services;
    }
}
