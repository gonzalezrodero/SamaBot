using Alba;
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
using Wolverine.Http;

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

        Host = await AlbaHost.For<Program>(builder =>
        {
            builder.UseSetting("ConnectionStrings:Marten", postgres.GetConnectionString());

            builder.ConfigureServices(services =>
            {
                services.Configure<WhatsAppOptions>(opts =>
                {
                    opts.AccessToken = "integration_test_access_token";
                    opts.PhoneNumberId = "integration_test_phone_id";
                    opts.AppSecret = "integration_test_secret";
                    opts.VerifyToken = "integration_test_verify_token";
                });

                services.AddWolverineHttp();
                services.AddNpgsqlDataSource(postgres.GetConnectionString());

                services.ConfigureMarten(opts =>
                {
                    opts.Connection(postgres.GetConnectionString());
                    opts.Schema.For<DocumentChunk>();
                });

                EmbeddingMock.Setup(x => x.GenerateAsync(
                        It.IsAny<IEnumerable<string>>(),
                        It.IsAny<EmbeddingGenerationOptions>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(new float[768])]));

                services.AddSingleton(EmbeddingMock.Object); services.AddScoped<IWhatsAppPayloadProcessor, WhatsAppPayloadProcessor>();

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

    public async Task DisposeAsync()
    {
        if (Host != null) await Host.DisposeAsync();
        await postgres.DisposeAsync();
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationAppFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}