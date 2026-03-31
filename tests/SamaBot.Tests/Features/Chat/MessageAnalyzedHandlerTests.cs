using AwesomeAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using SamaBot.Api.Core.Events;
using SamaBot.Tests.Extensions;

namespace SamaBot.Tests.Features.Chat;

[Collection("Integration")]
public class MessageAnalyzedHandlerTests(IntegrationAppFixture fixture)
{
    [Fact]
    public async Task GivenAnalyzedMessage_WhenHandlerRuns_ThenItGeneratesReplyAndAppendsToStream()
    {
        // Arrange
        var testPhone = "34888777666";
        var incomingEvent = new MessageAnalyzed(
            MessageId: "atomic.Chat1",
            PhoneNumber: testPhone,
            LanguageCode: "ca",
            OriginalText: "Quina és la contrasenya?"
        );

        // Act: Directly invoke the message bypassing HTTP and upstream handlers
        await fixture.Host.InvokeMessageAndWaitAsync(incomingEvent);

        // Assert: Verify the outcome of the Chat/RAG handler
        using var session = fixture.Host.Services.GetRequiredService<IDocumentStore>().LightweightSession();
        var streamEvents = await session.Events.FetchStreamAsync(testPhone);

        var replyGenerated = streamEvents.FirstOrDefault(e => e.Data is ReplyGenerated)?.Data as ReplyGenerated;

        replyGenerated.Should().NotBeNull("The handler should have appended a ReplyGenerated event to the stream.");

        // Asserting the fallback mock value configured in IntegrationAppFixture
        replyGenerated!.ReplyText.Should().Be("es");
        replyGenerated.MessageId.Should().Be("atomic.Chat1");
        replyGenerated.PhoneNumber.Should().Be(testPhone);
    }
}