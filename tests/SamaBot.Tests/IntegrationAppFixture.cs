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
using SamaBot.Api.Features.Knowledge;
using SamaBot.Api.Features.WhatsAppDispatcher;
using System.Text;
using Testcontainers.PostgreSql;
using Wolverine;

namespace SamaBot.Tests;

public class IntegrationAppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
        .WithDatabase("samabot_test")
        .Build();

    public IAlbaHost Host { get; private set; } = null!;

    // 🚀 SOLO MOCKEAMOS LA INFRAESTRUCTURA EXTERNA
    public Mock<IEmbeddingService> EmbeddingMock { get; } = new();
    public Mock<IAmazonBedrockRuntime> BedrockClientMock { get; } = new();
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
            builder.UseDefaultServiceProvider(options => options.ValidateScopes = false);

            builder.ConfigureServices(services =>
            {
                SetupMockResponses();

                services.Replace(ServiceDescriptor.Singleton(BedrockClientMock.Object));
                services.Replace(ServiceDescriptor.Singleton(EmbeddingMock.Object));
                services.Replace(ServiceDescriptor.Singleton(WhatsAppClientMock.Object));

                services.Configure<WolverineOptions>(opts => opts.AutoBuildMessageStorageOnStartup = AutoCreate.CreateOrUpdate);
                services.Configure<StoreOptions>(opts => {
                    opts.AutoCreateSchemaObjects = AutoCreate.All;
                    opts.Schema.For<DocumentChunk>();
                });
                services.Configure<WhatsAppOptions>(opts => {
                    opts.AccessToken = "integration_test_access_token";
                    opts.BaseUrl = "https://dummy-whatsapp-api.com";
                    opts.PhoneNumberId = "integration_test_phone_id";
                    opts.AppSecret = "integration_test_secret";
                    opts.VerifyToken = "integration_test_verify_token";
                });
            });
        });
    }

    private void SetupMockResponses()
    {
        var vector512 = new float[512]; vector512[0] = 0.1f;
        EmbeddingMock.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(vector512);

        WhatsAppClientMock.Setup(x => x.SendMessageAsync(It.IsAny<string>(), It.IsAny<WhatsAppTextRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppResponse("ok", [], []));

        BedrockClientMock.Setup(x => x.InvokeModelAsync(It.IsAny<InvokeModelRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvokeModelRequest request, CancellationToken ct) =>
            {
                var requestJson = Encoding.UTF8.GetString(request.Body.ToArray());

                string aiTextToReturn;

                if (requestJson.Contains("language detection module"))
                {
                    aiTextToReturn = "es";
                }
                else
                {
                    aiTextToReturn = "Mocked AI Response: Soy SamaBot y esto es un test E2E.";
                }

                var jsonResponse = $$"""
                {
                    "content": [ { "text": "{{aiTextToReturn}}" } ]
                }
                """;

                return new InvokeModelResponse
                {
                    HttpStatusCode = System.Net.HttpStatusCode.OK,
                    Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonResponse)) { Position = 0 }
                };
            });
    }

    public async Task DisposeAsync()
    {
        if (Host != null) await Host.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationAppFixture> { }