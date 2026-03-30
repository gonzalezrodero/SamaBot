using SamaBot.Api.Features.Knowledge;
using SamaBot.Api.Features.LanguageDetection;
using SamaBot.Api.Features.WhatsAppWebhook;

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
        services.AddLanguageDetectionFeature();
        services.AddKnowledgeFeature();

        return services;
    }
}
