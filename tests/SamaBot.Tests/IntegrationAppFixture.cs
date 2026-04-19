using Alba;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using JasperFx;
using Marten;
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
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
        .WithDatabase("samabot_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public IAlbaHost Host { get; private set; } = null!;

    public Mock<IEmbeddingService> EmbeddingMock { get; } = new();
    public Mock<IChatService> ChatMock { get; } = new();
    public Mock<IAmazonBedrockRuntime> BedrockClientMock { get; } = new();

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();

        // Environment variables to satisfy the SDK internals during builder startup
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("ConnectionStrings__Marten", postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("AWS_REGION", "eu-west-1");
        Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "testing");
        Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "testing");
        Environment.SetEnvironmentVariable("BedrockSettings__ModelId", "dummy-model");
        Environment.SetEnvironmentVariable("BedrockSettings__MaxTokens", "1000");

        try
        {
            Host = await AlbaHost.For<Program>(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // 1. SETUP MOCKS
                    EmbeddingMock.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new float[512]);

                    ChatMock.Setup(c => c.GetResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync("Mocked AI Response: Soy SamaBot y esto es un test E2E.");

                    // Mock the raw AWS Client to stop HTTP signing completely.
                    // Providing a dummy JSON body so JsonDocument.Parse doesn't fail if evaluated.
                    BedrockClientMock
                        .Setup(x => x.InvokeModelAsync(It.IsAny<InvokeModelRequest>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(() =>
                        {
                            // Creamos un JSON válido tanto para Embeddings como para Chat (Claude)
                            var jsonResponse = """
                            {
                                "embedding": [0.1, 0.2, 0.3],
                                "content": [ { "text": "Mocked AI Response from Bedrock SDK" } ]
                            }
                            """;

                            var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonResponse))
                            {
                                Position = 0
                            };

                            return new InvokeModelResponse
                            {
                                HttpStatusCode = System.Net.HttpStatusCode.OK,
                                Body = stream
                            };
                        });

                    var languageDetectorMock = new Mock<ILanguageDetector>();
                    languageDetectorMock.Setup(l => l.DetectLanguageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync("es");

                    var whatsappClientMock = new Mock<IWhatsAppClient>();
                    whatsappClientMock.Setup(client => client.SendMessageAsync(It.IsAny<string>(), It.IsAny<WhatsAppTextRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new WhatsAppResponse("whatsapp", [], []));

                    // 2. NUCLEAR REMOVAL (Clear the slate)
                    services.RemoveAll<IAmazonBedrockRuntime>();
                    services.RemoveAll<IEmbeddingService>();
                    services.RemoveAll<IChatService>();
                    services.RemoveAll<ILanguageDetector>();
                    services.RemoveAll<IWhatsAppClient>();

                    // 3. INJECT MOCKS AS THE ONLY SOURCE OF TRUTH
                    services.AddSingleton(BedrockClientMock.Object);
                    services.AddSingleton(EmbeddingMock.Object);
                    services.AddSingleton(ChatMock.Object);
                    services.AddSingleton(languageDetectorMock.Object);
                    services.AddSingleton(whatsappClientMock.Object);

                    // 4. WOLVERINE INTERNAL CONTAINER OVERRIDES
                    services.Configure<WolverineOptions>(opts =>
                    {
                        opts.AutoBuildMessageStorageOnStartup = AutoCreate.CreateOrUpdate;

                        opts.Services.RemoveAll<IAmazonBedrockRuntime>();
                        opts.Services.RemoveAll<IEmbeddingService>();
                        opts.Services.RemoveAll<IChatService>();

                        opts.Services.AddSingleton(BedrockClientMock.Object);
                        opts.Services.AddSingleton(EmbeddingMock.Object);
                        opts.Services.AddSingleton(ChatMock.Object);
                    });

                    // 5. INFRASTRUCTURE SETTINGS
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
        catch (Exception ex)
        {
            throw new Exception($"FATAL ERROR IN FIXTURE: {ex.Message}", ex);
        }
    }

    public async Task DisposeAsync()
    {
        if (Host != null) await Host.DisposeAsync();
        await postgres.DisposeAsync();

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__Marten", null);
        Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", null);
        Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", null);
        Environment.SetEnvironmentVariable("AWS_REGION", null);
        Environment.SetEnvironmentVariable("BedrockSettings__ModelId", null);
        Environment.SetEnvironmentVariable("BedrockSettings__MaxTokens", null);
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationAppFixture>
{
}