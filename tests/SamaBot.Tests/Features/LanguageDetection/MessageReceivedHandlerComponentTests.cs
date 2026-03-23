using AwesomeAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using SamaBot.Api.Core.Events;
using SamaBot.Tests.Extensions;

namespace SamaBot.Tests.Features.LanguageDetection;

[Collection("Integration")]
public class MessageReceivedHandlerComponentTests(IntegrationAppFixture fixture)
{
    [Fact]
    public async Task GivenRawMessage_WhenHandlerRuns_ThenItResolvesLanguageAndAppendsMessageAnalyzedEvent()
    {
        // Arrange
        var testPhone = "34111222333";
        var incomingEvent = new MessageReceived(
            MessageId: "atomic.Test1",
            BotPhoneNumberId: "123",
            PhoneNumber: testPhone,
            Text: "Hola amics",
            ReceivedAt: DateTimeOffset.UtcNow
        );

        // Act: Directly invoke the message bypassing HTTP (Atomic Component Test)
        await fixture.Host.InvokeMessageAndWaitAsync(incomingEvent);

        // Assert: Verify only the outcome of this specific handler
        using var session = fixture.Host.Services.GetRequiredService<IDocumentStore>().LightweightSession();
        var streamEvents = await session.Events.FetchStreamAsync(testPhone);
        
        var messageAnalyzed = streamEvents.FirstOrDefault(e => e.Data is MessageAnalyzed)?.Data as MessageAnalyzed;
        
        messageAnalyzed.Should().NotBeNull();
        messageAnalyzed!.LanguageCode.Should().Be("es"); // Hardcoded response in our StubChatClient
        messageAnalyzed.MessageId.Should().Be("atomic.Test1");
    }
}
