using Alba;
using JasperFx;
using Marten;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SamaBot.Api.Common.Configuration;
using SamaBot.Api.Core.Entities;
using SamaBot.Api.Features.LanguageDetection;
using SamaBot.Api.Features.WhatsAppDispatcher;
using SamaBot.Api.Features.WhatsAppWebhook;
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

    public Mock<IEmbeddingGenerator<string, Embedding<float>>> EmbeddingMock { get; } = new();

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("ConnectionStrings__Marten", postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Ollama__BaseUrl", "http://localhost:11434");

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

                    // --- MOCKS ---
                    EmbeddingMock.Setup(x => x.GenerateAsync(
                            It.IsAny<IEnumerable<string>>(),
                            It.IsAny<EmbeddingGenerationOptions>(),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(new float[768])]));

                    services.AddSingleton(EmbeddingMock.Object);
                    services.AddScoped<IWhatsAppPayloadProcessor, WhatsAppPayloadProcessor>();

                    var chatClientMock = new Mock<IChatClient>();
                    var mockResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "es"));
                    chatClientMock.Setup(c => c.GetResponseAsync(
                            It.IsAny<IEnumerable<ChatMessage>>(),
                            It.IsAny<ChatOptions>(),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(mockResponse);

                    services.AddSingleton(chatClientMock.Object);

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
            throw new Exception($"FATAL ERROR: {ex.Message}", ex);
        }
    }

    public async Task DisposeAsync()
    {
        if (Host != null) await Host.DisposeAsync();
        await postgres.DisposeAsync();

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__Marten", null);
        Environment.SetEnvironmentVariable("Ollama__BaseUrl", null);
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationAppFixture>
{
}