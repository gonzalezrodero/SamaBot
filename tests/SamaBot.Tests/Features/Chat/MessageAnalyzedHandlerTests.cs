using Amazon.BedrockRuntime.Model;
using AwesomeAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Moq;
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

        // Asserting the mock value configured in IntegrationAppFixture
        // Updated to match the string in the fixture
        replyGenerated!.Text.Should().Be("Mocked AI Response: Soy SamaBot y esto es un test E2E.");
        replyGenerated.MessageId.Should().Be("atomic.Chat1");
        replyGenerated.PhoneNumber.Should().Be(testPhone);
    }

    [Fact]
    public async Task GivenPreviousConversation_WhenHandlerRuns_ThenItUsesHistoryAndAppendsReply()
    {
        // Arrange
        var testPhone = "34999555111"; // Unique phone number for this test
        using var session = fixture.Host.Services.GetRequiredService<IDocumentStore>().LightweightSession();

        // 1. Pre-populate the Marten Event Store with a past conversation
        session.Events.Append(testPhone, new MessageReceived("old.1", "123", testPhone, "Hola", DateTimeOffset.UtcNow));
        session.Events.Append(testPhone, new ReplyGenerated("old.1", testPhone, "¡Hola! Soy SamàBot."));
        session.Events.Append(testPhone, new MessageReceived("atomic.Chat2", "123", testPhone, "¿Me recuerdas?", DateTimeOffset.UtcNow));
        await session.SaveChangesAsync();

        // 2. The new incoming message
        var incomingEvent = new MessageAnalyzed(
            MessageId: "atomic.Chat2",
            PhoneNumber: testPhone,
            LanguageCode: "es",
            OriginalText: "¿Me recuerdas?"
        );

        // Act
        await fixture.Host.InvokeMessageAndWaitAsync(incomingEvent);

        // Assert
        var streamEvents = await session.Events.FetchStreamAsync(testPhone);
        var replies = streamEvents.Select(e => e.Data).OfType<ReplyGenerated>().ToList();

        replies.Should().HaveCount(2, "There should be the old reply and the newly generated one.");
        replies.Last().MessageId.Should().Be("atomic.Chat2");

        // Verify that the Bedrock mock was invoked with a payload containing the full conversation history
        fixture.BedrockClientMock.Verify(c => c.InvokeModelAsync(It.Is<InvokeModelRequest>(req =>
            VerifyChatHistoryPayload(req)
        ), It.IsAny<CancellationToken>()), Times.Once, "The LLM payload must contain both the history and the new message.");
    }

    // Helper method to inspect the MemoryStream inside the Moq verification safely
    private static bool VerifyChatHistoryPayload(InvokeModelRequest request)
    {
        var requestJson = System.Text.Encoding.UTF8.GetString(request.Body.ToArray());

        return requestJson.Contains("Hola") &&
               requestJson.Contains("¡Hola! Soy SamàBot.") &&
               requestJson.Contains("¿Me recuerdas?");
    }
}