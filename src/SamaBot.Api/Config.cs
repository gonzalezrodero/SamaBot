using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Microsoft.Extensions.AI;
using Npgsql;
using OllamaSharp;
using SamaBot.Api.Common.Configuration;
using SamaBot.Api.Features.Knowledge;
using SamaBot.Api.Features.LanguageDetection;
using SamaBot.Api.Features.WhatsAppDispatcher;
using SamaBot.Api.Features.WhatsAppWebhook;
using Wolverine.Marten;

namespace SamaBot.Api;

public static class Config
{
    // --- FEATURE REGISTRATION ---
    public static IServiceCollection AddFeatures(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WhatsAppOptions>(configuration.GetSection(WhatsAppOptions.SectionName));

        services.AddWhatsAppWebhookFeature();
        services.AddLanguageDetectionFeature();
        services.AddKnowledgeFeature();
        services.AddWhatsAppDispatcherFeature(configuration);

        return services;
    }

    // --- DATABASE & MARTEN REGISTRATION ---
    public static IServiceCollection AddDatabase(this IServiceCollection services, string connectionString)
    {
        services.AddNpgsqlDataSource(connectionString);

        services.AddMarten(opts =>
        {
            opts.Events.StreamIdentity = StreamIdentity.AsString;
            opts.Storage.Add(new HnswIndexCustomizer());
            opts.Projections.Add<ProcessedMessageProjection>(ProjectionLifecycle.Inline);
        })
        .ApplyAllDatabaseChangesOnStartup()
        .UseNpgsqlDataSource()
        .UseLightweightSessions()
        .IntegrateWithWolverine(cfg => cfg.UseWolverineManagedEventSubscriptionDistribution = true);

        return services;
    }

    // --- AI & LLM REGISTRATION ---
    public static IServiceCollection AddAi(this IServiceCollection services, string ollamaUrl)
    {
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
            new OllamaApiClient(new Uri(ollamaUrl), "nomic-embed-text"));

        services.AddSingleton<IChatClient>(
            new OllamaApiClient(new Uri(ollamaUrl), "llama3"));

        return services;
    }

    // --- INFRASTRUCTURE INITIALIZATION ---
    public static WebApplication EnsureVectorExtensionExists(this WebApplication app, string connectionString)
    {
        // Ensure the pgvector extension exists before Marten tries to use it
        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE EXTENSION IF NOT EXISTS vector;";
        cmd.ExecuteNonQuery();

        return app;
    }
}