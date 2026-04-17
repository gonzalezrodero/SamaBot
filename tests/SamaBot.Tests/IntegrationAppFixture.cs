using Alba;
using JasperFx;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SamaBot.Api.Common.Configuration;
using SamaBot.Api.Core.Entities;
using SamaBot.Api.Features.Chat;
using SamaBot.Api.Features.Knowledge;
using SamaBot.Api.Features.LanguageDetection;
using SamaBot.Api.Features.WhatsAppDispatcher;
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

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();

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
                    services.Configure<StoreOptions>(opts =>
                    {
                        opts.AutoCreateSchemaObjects = AutoCreate.All;
                        opts.Schema.For<DocumentChunk>();
                    });

                    services.Configure<WolverineOptions>(opts =>
                    {
                        opts.AutoBuildMessageStorageOnStartup = AutoCreate.CreateOrUpdate;
                    });

                    services.Configure<WhatsAppOptions>(opts =>
                    {
                        opts.AccessToken = "integration_test_access_token";
                        opts.BaseUrl = "https://dummy-whatsapp-api.com";
                        opts.PhoneNumberId = "integration_test_phone_id";
                        opts.AppSecret = "integration_test_secret";
                        opts.VerifyToken = "integration_test_verify_token";
                    });


                    EmbeddingMock.Setup(x => x.GenerateEmbeddingAsync(
                            It.IsAny<string>(),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new float[512]);

                    services.AddSingleton(EmbeddingMock.Object);

                    ChatMock.Setup(c => c.GetResponseAsync(
                            It.IsAny<string>(),
                            It.IsAny<string>(),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync("Mocked AI Response: Soy SamaBot y esto es un test E2E.");

                    services.AddSingleton(ChatMock.Object);

                    var languageDetectorMock = new Mock<ILanguageDetector>();
                    languageDetectorMock.Setup(l => l.DetectLanguageAsync(
                            It.IsAny<string>(),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync("es");

                    services.AddSingleton(languageDetectorMock.Object);

                    var whatsappClientMock = new Mock<IWhatsAppClient>();
                    whatsappClientMock.Setup(client => client.SendMessageAsync(
                            It.IsAny<string>(),
                            It.IsAny<WhatsAppTextRequest>(),
                            It.IsAny<string>(),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new WhatsAppResponse("whatsapp", [], []));

                    services.AddSingleton(whatsappClientMock.Object);
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

        // Limpieza de variables de entorno
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