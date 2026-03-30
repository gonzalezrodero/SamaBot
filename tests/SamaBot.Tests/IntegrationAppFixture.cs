using Alba;
using Marten;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SamaBot.Api.Features.LanguageDetection;
using SamaBot.Api.Features.WhatsAppWebhook;
using Testcontainers.PostgreSql;
using Wolverine.Http;

namespace SamaBot.Tests;

public class IntegrationAppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
        .WithDatabase("samabot_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public IAlbaHost Host { get; private set; } = null!;

    public Mock<IEmbeddingGenerator<string, Embedding<float>>> EmbeddingMock { get; } = new();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Host = await AlbaHost.For<Program>(builder =>
        {
            builder.UseSetting("WhatsApp:App_Secret", "TEST_APP_SECRET_FOR_E2E_ONLY");
            builder.UseSetting("ConnectionStrings:Marten", _postgres.GetConnectionString());

            builder.ConfigureServices(services =>
            {
                services.AddWolverineHttp();
                services.AddNpgsqlDataSource(_postgres.GetConnectionString());

                services.ConfigureMarten(opts =>
                {
                    opts.Connection(_postgres.GetConnectionString());
                });

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
            });
        });
    }

    public async Task DisposeAsync()
    {
        if (Host != null) await Host.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationAppFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}