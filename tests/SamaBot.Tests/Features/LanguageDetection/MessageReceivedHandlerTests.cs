using AwesomeAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using SamaBot.Api.Core.Events;
using SamaBot.Tests.Extensions;

namespace SamaBot.Tests.Features.LanguageDetection;

[Collection("Integration")]
public class MessageReceivedHandlerTests(IntegrationAppFixture fixture)
{
    [Fact]
    public async Task GivenRawMessage_WhenHandlerRuns_ThenItResolvesLanguageAndAppendsMessageAnalyzedEvent()
    {
        // Arrange
        var testPhone = "34111222333";
        var botPhone = "123";
        var tenantId = "club-sama";

        var incomingEvent = new MessageReceived(
            MessageId: "atomic.Test1",
            PhoneNumber: testPhone,
            Text: "Hola amics",
            TenantId: tenantId,      
            BotPhoneNumberId: botPhone,
            ReceivedAt: DateTimeOffset.UtcNow
        );

        // Act
        await fixture.Host.InvokeMessageAndWaitAsync(incomingEvent);

        // Assert
        using var session = fixture.Host.Services.GetRequiredService<IDocumentStore>().LightweightSession(tenantId);
        var streamEvents = await session.Events.FetchStreamAsync(testPhone);

        var messageAnalyzed = streamEvents.FirstOrDefault(e => e.Data is MessageAnalyzed)?.Data as MessageAnalyzed;

        messageAnalyzed.Should().NotBeNull();
        messageAnalyzed!.LanguageCode.Should().Be("en");
        messageAnalyzed.MessageId.Should().Be("atomic.Test1");
        messageAnalyzed.TenantId.Should().Be(tenantId);
    }
}