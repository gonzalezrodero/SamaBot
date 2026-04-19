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

    // 🚀 EXPOSE ALL MOCKS GLOBALLY
    public Mock<IEmbeddingService> EmbeddingMock { get; } = new();
    public Mock<IChatService> ChatMock { get; } = new();
    public Mock<IAmazonBedrockRuntime> BedrockClientMock { get; } = new();
    public Mock<ILanguageDetector> LanguageDetectorMock { get; } = new();
    public Mock<IWhatsAppClient> WhatsAppClientMock { get; } = new();

    private static readonly float[] MockVector = [.. (new[] { 0.1f }), .. Enumerable.Repeat(0f, 511)];

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
                    // 1. SETUP ALL MOCKS
                    EmbeddingMock.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(MockVector);

                    ChatMock.Setup(c => c.GetResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync("Mocked AI Response: Soy SamaBot y esto es un test E2E.");

                    LanguageDetectorMock.Setup(l => l.DetectLanguageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync("es");

                    WhatsAppClientMock.Setup(client => client.SendMessageAsync(It.IsAny<string>(), It.IsAny<WhatsAppTextRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new WhatsAppResponse("whatsapp", [], []));

                    // Bedrock nuclear mock
                    BedrockClientMock
                        .Setup(x => x.InvokeModelAsync(It.IsAny<InvokeModelRequest>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(() =>
                        {
                            var mockVector = new float[512];
                            mockVector[0] = 0.1f;
                            var vectorJson = System.Text.Json.JsonSerializer.Serialize(mockVector);

                            var jsonResponse = $$"""
                            {
                                "embedding": {{vectorJson}},
                                "content": [ { "text": "Mocked AI Response: Soy SamaBot y esto es un test E2E." } ]
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


                    // 2. NUCLEAR OVERRIDE
                    services.Replace(ServiceDescriptor.Singleton<IAmazonBedrockRuntime>(BedrockClientMock.Object));
                    services.Replace(ServiceDescriptor.Singleton<IEmbeddingService>(EmbeddingMock.Object));
                    services.Replace(ServiceDescriptor.Singleton<IChatService>(ChatMock.Object));
                    services.Replace(ServiceDescriptor.Singleton<ILanguageDetector>(LanguageDetectorMock.Object));
                    services.Replace(ServiceDescriptor.Singleton<IWhatsAppClient>(WhatsAppClientMock.Object));

                    // 3. BORRAR CLASES CONCRETAS: 
                    services.RemoveAll<EmbeddingService>();
                    services.RemoveAll<ChatService>();
                    services.RemoveAll<LanguageDetector>();

                    // 4. WOLVERINE INTERNAL CONTAINER OVERRIDES
                    services.Configure<WolverineOptions>(opts =>
                    {
                        opts.AutoBuildMessageStorageOnStartup = AutoCreate.CreateOrUpdate;

                        opts.Services.Replace(ServiceDescriptor.Singleton<IAmazonBedrockRuntime>(BedrockClientMock.Object));
                        opts.Services.Replace(ServiceDescriptor.Singleton<IEmbeddingService>(EmbeddingMock.Object));
                        opts.Services.Replace(ServiceDescriptor.Singleton<IChatService>(ChatMock.Object));
                        opts.Services.Replace(ServiceDescriptor.Singleton<ILanguageDetector>(LanguageDetectorMock.Object));
                        opts.Services.Replace(ServiceDescriptor.Singleton<IWhatsAppClient>(WhatsAppClientMock.Object));

                        opts.Services.RemoveAll<EmbeddingService>();
                        opts.Services.RemoveAll<ChatService>();
                        opts.Services.RemoveAll<LanguageDetector>();
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