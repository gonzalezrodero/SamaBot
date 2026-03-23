namespace SamaBot.Api.Features.WhatsAppWebhook;

public static class Config
{
    public static IServiceCollection AddWhatsAppWebhookFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IWhatsAppPayloadProcessor, WhatsAppPayloadProcessor>();
        
        // Settings bindings or other feature-specific services could also go here
        
        return services;
    }
}
