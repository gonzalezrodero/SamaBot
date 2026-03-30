namespace SamaBot.Api.Features.Knowledge;

public static class Config
{
    public static IServiceCollection AddKnowledgeFeature(this IServiceCollection services)
    {
        services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();
        services.AddScoped<IPdfIngestionService, PdfIngestionService>();
        return services;
    }
}