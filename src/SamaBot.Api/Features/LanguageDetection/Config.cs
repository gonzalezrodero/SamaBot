namespace SamaBot.Api.Features.LanguageDetection;

public static class Config
{
    public static IServiceCollection AddLanguageDetectionFeature(this IServiceCollection services)
    {
        services.AddScoped<ILanguageDetector, LanguageDetector>();
        
        return services;
    }
}
