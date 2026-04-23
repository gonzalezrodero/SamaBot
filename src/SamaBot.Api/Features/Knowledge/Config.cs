using SamaBot.Api.Features.Knowledge.Extractors;
using SamaBot.Api.Features.Knowledge.Services;

namespace SamaBot.Api.Features.Knowledge;

public static class Config
{
    public static IServiceCollection AddKnowledgeFeature(this IServiceCollection services)
    {
        services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();
        services.AddTransient<IKnowledgeIngestionService, KnowledgeIngestionService>();
        services.AddTransient<IDocumentExtractor, TextDocumentExtractor>();
        services.AddTransient<IDocumentExtractor, PdfDocumentExtractor>();
        
        return services;
    }
}