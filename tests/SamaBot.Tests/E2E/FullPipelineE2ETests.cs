using Alba;
using AwesomeAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using SamaBot.Api.Core.Events;
using System.Security.Cryptography;
using System.Text;
using Wolverine.Tracking;

namespace SamaBot.Tests.E2E;

[Collection("Integration")]
public class FullPipelineE2ETests(IntegrationAppFixture fixture)
{
    [Fact]
    public async Task CompleteUserJourney_FromWebhookToFinalCascade()
    {
        // Arrange
        var payload = """
        {
          "object": "whatsapp_business_account",
          "entry": [ { "changes": [ { "value": {
            "metadata": { "phone_number_id": "12345" },
            "messages": [ { "from": "34999888777", "id": "wamid.E2E", "timestamp": "1603059201", "text": { "body": "End to End Text" }, "type": "text" } ]
          } } ] } ]
        }
        """;

        var configSecret = "TU_APP_SECRET_QUE_ESTA_EN_BASIC_SETTINGS"; 
        
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(configSecret));
        var signature = "sha256=" + Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));

        // Act
        // This simulates a real WhatsApp HTTP POST and waits for EVERY cascading handler in the system to finish
        await fixture.Host.ExecuteAndWaitAsync(async () =>
        {
            await fixture.Host.Scenario(s =>
            {
                s.Post.Text(payload).ContentType("application/json").ToUrl("/api/whatsapp/webhook");
                s.WithRequestHeader("X-Hub-Signature-256", signature);
                s.StatusCodeShouldBeOk();
            });
        });

        // Assert E2E State
        using var session = fixture.Host.Services.GetRequiredService<IDocumentStore>().LightweightSession();
        var streamEvents = await session.Events.FetchStreamAsync("34999888777");
        
        streamEvents.Should().NotBeEmpty();
        
        // 1. Webhook Phase
        streamEvents.Any(e => e.Data is MessageReceived).Should().BeTrue("The webhook should have recorded the intake.");
        
        // 2. Language Detection Phase
        streamEvents.Any(e => e.Data is MessageAnalyzed).Should().BeTrue("The NLP pipeline should have cascaded and resolved.");
        
        // Note: As we add Phase 5 (RAG) and Phase 6 (Dispatch), we will add simple presence assertions here
        // to guarantee the ENTIRE chain fired effectively from a single HTTP call.
    }
}
