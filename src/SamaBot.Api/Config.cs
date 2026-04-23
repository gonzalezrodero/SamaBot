using Amazon.BedrockRuntime;
using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Marten.Storage;
using Npgsql;
using SamaBot.Api.Common.Configuration;
using SamaBot.Api.Core.Entities;
using SamaBot.Api.Features.Chat;
using SamaBot.Api.Features.Knowledge;
using SamaBot.Api.Features.LanguageDetection;
using SamaBot.Api.Features.WhatsAppDispatcher;
using SamaBot.Api.Features.WhatsAppWebhook;
using Wolverine.Marten;

namespace SamaBot.Api;

public static class Config
{
    public static IServiceCollection AddFeatures(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WhatsAppOptions>(configuration.GetSection(WhatsAppOptions.SectionName));

        services.AddWhatsAppWebhookFeature();
        services.AddLanguageDetectionFeature();
        services.AddKnowledgeFeature();
        services.AddWhatsAppDispatcherFeature(configuration);

        return services;
    }

    public static IServiceCollection AddDatabase(this IServiceCollection services, string connectionString)
    {
        services.AddNpgsqlDataSource(connectionString);
        services.CritterStackDefaults(opts =>
        {
            opts.Development.GeneratedCodeMode = TypeLoadMode.Auto;
            opts.Production.GeneratedCodeMode = TypeLoadMode.Static;
        });

        services.AddMarten(opts =>
        {
            opts.Connection(connectionString);
            opts.Events.StreamIdentity = StreamIdentity.AsString;
            opts.Events.TenancyStyle = TenancyStyle.Conjoined;

            opts.Policies.AllDocumentsAreMultiTenanted();

            opts.Storage.Add(new HnswIndexCustomizer());
            opts.Projections.Add<ProcessedMessageProjection>(ProjectionLifecycle.Inline);
            opts.Schema.For<DocumentChunk>();
        })
        .ApplyAllDatabaseChangesOnStartup()
        .UseNpgsqlDataSource()
        .UseLightweightSessions()
        .IntegrateWithWolverine(cfg => cfg.UseWolverineManagedEventSubscriptionDistribution = true);

        return services;
    }

    public static IServiceCollection AddAi(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind settings from appsettings.json
        services.Configure<BedrockSettings>(configuration.GetSection("BedrockSettings"));

        // Register AWS Bedrock Client (It will automatically use the ECS Task Role)
        services.AddDefaultAWSOptions(configuration.GetAWSOptions());
        services.AddAWSService<IAmazonBedrockRuntime>();

        // Register our custom Bedrock services
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IEmbeddingService, EmbeddingService>();

        return services;
    }

    public static WebApplication EnsureVectorExtensionExists(this WebApplication app, string connectionString)
    {
        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            CREATE EXTENSION IF NOT EXISTS vector;

            CREATE OR REPLACE FUNCTION public.extract_embedding(data jsonb) 
            RETURNS vector IMMUTABLE PARALLEL SAFE AS $$
            BEGIN
                -- Ensure 'Embedding' matches your C# property name exactly
                RETURN CAST(data ->> 'Embedding' AS vector(512));
            EXCEPTION WHEN OTHERS THEN
                -- Fallback to a zero vector to avoid crashing the index
                RETURN array_fill(0, ARRAY[512])::vector;
            END;
            $$ LANGUAGE plpgsql;";

        cmd.ExecuteNonQuery();

        return app;
    }
}