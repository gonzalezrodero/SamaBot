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
using SamaBot.Api.Features.WhatsAppDispatcher;
using SamaBot.Api.Features.WhatsAppWebhook;
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

    // 🚀 TU IDEA: Solo mockeamos las fronteras externas. Ni ChatService ni LanguageDetector.
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

                // 🚀 EL FIX CRÍTICO: Poner el tipo explícito <IAmazonBedrockRuntime>
                // Esto garantiza que sobreescribimos el cliente real y evitamos que vaya a AWS.
                services.Replace(ServiceDescriptor.Singleton<IAmazonBedrockRuntime>(BedrockClientMock.Object));
                services.Replace(ServiceDescriptor.Singleton<IWhatsAppClient>(WhatsAppClientMock.Object));

                services.Configure<WolverineOptions>(opts =>
                    opts.AutoBuildMessageStorageOnStartup = AutoCreate.CreateOrUpdate);

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
        WhatsAppClientMock.Setup(x => x.SendMessageAsync(It.IsAny<string>(), It.IsAny<WhatsAppTextRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppResponse("ok", [], []));

        BedrockClientMock.Setup(x => x.InvokeModelAsync(It.IsAny<InvokeModelRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvokeModelRequest request, CancellationToken ct) =>
            {
                var requestJson = Encoding.UTF8.GetString(request.Body.ToArray());
                string jsonResponse;

                if (requestJson.Contains("inputText") || requestJson.Contains("texts") || (request.ModelId?.Contains("embed") ?? false))
                {
                    var vector512 = new float[512];
                    vector512[0] = 0.1f;
                    var vectorJson = System.Text.Json.JsonSerializer.Serialize(vector512);

                    jsonResponse = $$"""
                    {
                        "embedding": {{vectorJson}},
                        "embeddings": [{{vectorJson}}]
                    }
                    """;
                }
                else if (requestJson.Contains("language detection module"))
                {
                    jsonResponse = $$"""
                    {
                        "content": [ { "text": "es" } ]
                    }
                    """;
                }
                else
                {
                    jsonResponse = $$"""
                    {
                        "content": [ { "text": "Mocked AI Response: Soy SamaBot y esto es un test E2E." } ]
                    }
                    """;
                }

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