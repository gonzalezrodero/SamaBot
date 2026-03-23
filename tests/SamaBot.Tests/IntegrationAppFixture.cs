using Alba;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using SamaBot.Api;
using Testcontainers.PostgreSql;
using Wolverine.Http;
using Moq;

namespace SamaBot.Tests;

public class IntegrationAppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("samabot_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public IAlbaHost Host { get; private set; } = null!;
    
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Host = await AlbaHost.For<Program>(builder =>
        {
            // Override the connection string targeting Testcontainers and inject MEAI shims
            builder.ConfigureServices(services =>
            {
                services.AddWolverineHttp(); // CRITICAL: Explicitly ensure Wolverine HTTP is loaded for Alba context
                
                var chatClientMock = new Mock<Microsoft.Extensions.AI.IChatClient>();
                var mockResponse = new Microsoft.Extensions.AI.ChatResponse(new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.Assistant, "es"));
                chatClientMock.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<Microsoft.Extensions.AI.ChatMessage>>(), It.IsAny<Microsoft.Extensions.AI.ChatOptions>(), It.IsAny<CancellationToken>()))
                              .ReturnsAsync(mockResponse);

                // Override external AI provider with Fake to ensure stable execution
                services.AddSingleton<Microsoft.Extensions.AI.IChatClient>(chatClientMock.Object);
                
                services.ConfigureMarten(opts => 
                {
                    opts.Connection(_postgres.GetConnectionString());
                });
            });
        });
    }

    public async Task DisposeAsync()
    {
        if (Host != null)
        {
            await Host.DisposeAsync();
        }
        
        await _postgres.DisposeAsync();
    }
}

// xUnit Collection Definition
[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationAppFixture>
{
}
