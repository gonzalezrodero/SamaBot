using Alba;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using JasperFx;
using Marten;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using SamaBot.Api.Common.Configuration;
using SamaBot.Api.Core.Entities;
using SamaBot.Api.Features.Chat;
using SamaBot.Api.Features.Knowledge;
using SamaBot.Api.Features.LanguageDetection;
using SamaBot.Api.Features.WhatsAppDispatcher;
using System.Text;
using Testcontainers.PostgreSql;
using Wolverine;

namespace SamaBot.Tests;

public class IntegrationAppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
        .WithDatabase("samabot_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public IAlbaHost Host { get; private set; } = null!;

    // Mocks expuestos para ser configurados en los tests si es necesario
    public Mock<IEmbeddingService> EmbeddingMock { get; } = new();
    public Mock<IChatService> ChatMock { get; } = new();
    public Mock<IAmazonBedrockRuntime> BedrockClientMock { get; } = new();
    public Mock<ILanguageDetector> LanguageDetectorMock { get; } = new();
    public Mock<IWhatsAppClient> WhatsAppClientMock { get; } = new();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("ConnectionStrings__Marten", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("AWS_REGION", "eu-west-1");
        Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "testing");
        Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "testing");
        Environment.SetEnvironmentVariable("BedrockSettings__ModelId", "dummy-model");

        Host = await AlbaHost.For<Program>(builder =>
        {
            builder.UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = false;
                options.ValidateOnBuild = false;
            });

            builder.ConfigureServices(services =>
            {
                SetupMocks();

                var typesToMock = new[]
                {
                    typeof(IAmazonBedrockRuntime), typeof(IEmbeddingService), typeof(EmbeddingService),
                    typeof(IChatService), typeof(ChatService), typeof(ILanguageDetector),
                    typeof(LanguageDetector), typeof(IWhatsAppClient)
                };

                foreach (var type in typesToMock) services.RemoveAll(type);

                services.AddSingleton(BedrockClientMock.Object);
                services.AddSingleton(EmbeddingMock.Object);
                services.AddSingleton(ChatMock.Object);
                services.AddSingleton(LanguageDetectorMock.Object);
                services.AddSingleton(WhatsAppClientMock.Object);

                services.Configure<WolverineOptions>(opts =>
                {
                    opts.AutoBuildMessageStorageOnStartup = AutoCreate.CreateOrUpdate;
                });

                services.Configure<StoreOptions>(opts =>
                {
                    opts.AutoCreateSchemaObjects = AutoCreate.All;
                    opts.Schema.For<DocumentChunk>();
                });

                services.Configure<WhatsAppOptions>(opts =>
                {
                    opts.AccessToken = "integration_test_access_token";
                    opts.BaseUrl = "https://dummy-whatsapp-api.com";
                    opts.PhoneNumberId = "integration_test_phone_id";
                    opts.AppSecret = "integration_test_secret";
                    opts.VerifyToken = "integration_test_verify_token";
                });
            });
        });
    }

    private void SetupMocks()
    {
        EmbeddingMock.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockVector());

        ChatMock.Setup(c => c.GetResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mocked AI Response: Soy SamaBot y esto es un test E2E.");

        LanguageDetectorMock.Setup(l => l.DetectLanguageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("es");

        WhatsAppClientMock.Setup(c => c.SendMessageAsync(It.IsAny<string>(), It.IsAny<WhatsAppTextRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppResponse("whatsapp", [], []));

        // Mock nuclear de Bedrock: Devuelve un JSON válido con un vector de 512 dimensiones
        BedrockClientMock.Setup(x => x.InvokeModelAsync(It.IsAny<InvokeModelRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var vectorJson = System.Text.Json.JsonSerializer.Serialize(CreateMockVector());
                var jsonResponse = $$"""
                {
                    "embedding": {{vectorJson}},
                    "content": [ { "text": "Mocked AI Response: Soy SamaBot y esto es un test E2E." } ]
                }
                """;
                return new InvokeModelResponse
                {
                    HttpStatusCode = System.Net.HttpStatusCode.OK,
                    Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonResponse)) { Position = 0 }
                };
            });
    }

    private static float[] CreateMockVector()
    {
        var v = new float[512];
        v[0] = 0.1f;
        return v;
    }

    public async Task DisposeAsync()
    {
        if (Host != null) await Host.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationAppFixture> { }