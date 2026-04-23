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
        var botPhone = "34111222333"; // TENANT ID
        var userPhone = "34888777666"; // STREAM ID

        var incomingEvent = new MessageAnalyzed(
            MessageId: "atomic.Chat1",
            BotPhoneNumberId: botPhone,
            PhoneNumber: userPhone,
            LanguageCode: "ca",
            OriginalText: "Quina és la contrasenya?"
        );

        // Act: Directly invoke the message bypassing HTTP and upstream handlers
        await fixture.Host.InvokeMessageAndWaitAsync(incomingEvent);

        // Assert: Verify the outcome of the Chat/RAG handler
        // 🚀 ABRIMOS LA SESIÓN DEL TENANT (botPhone) PARA LEER EL STREAM DEL USUARIO (userPhone)
        using var session = fixture.Host.Services.GetRequiredService<IDocumentStore>().LightweightSession(botPhone);
        var streamEvents = await session.Events.FetchStreamAsync(userPhone);

        var replyGenerated = streamEvents.FirstOrDefault(e => e.Data is ReplyGenerated)?.Data as ReplyGenerated;

        replyGenerated.Should().NotBeNull("The handler should have appended a ReplyGenerated event to the stream.");

        // Asserting the mock value configured in IntegrationAppFixture
        replyGenerated!.Text.Should().Be("Mocked AI Response: Soy SamaBot y esto es un test E2E.");
        replyGenerated.MessageId.Should().Be("atomic.Chat1");
        replyGenerated.BotPhoneNumberId.Should().Be(botPhone); // Verificamos que el BotId se propagó
        replyGenerated.PhoneNumber.Should().Be(userPhone);     // Verificamos que el UserPhone se propagó
    }

    [Fact]
    public async Task GivenPreviousConversation_WhenHandlerRuns_ThenItUsesHistoryAndAppendsReply()
    {
        // Arrange
        var botPhone = "34111222333"; // TENANT ID
        var userPhone = "34999555111"; // STREAM ID (Unique user phone number for this test)

        using var session = fixture.Host.Services.GetRequiredService<IDocumentStore>().LightweightSession(botPhone);

        // 1. Pre-populate the Marten Event Store with a past conversation
        // 🚀 Usamos los constructores actualizados con ambos IDs
        session.Events.Append(userPhone, new MessageReceived("old.1", botPhone, userPhone, "Hola", DateTimeOffset.UtcNow));
        session.Events.Append(userPhone, new ReplyGenerated("old.1", botPhone, userPhone, "¡Hola! Soy SamàBot."));
        session.Events.Append(userPhone, new MessageReceived("atomic.Chat2", botPhone, userPhone, "¿Me recuerdas?", DateTimeOffset.UtcNow));
        await session.SaveChangesAsync();

        // 2. The new incoming message
        var incomingEvent = new MessageAnalyzed(
            MessageId: "atomic.Chat2",
            BotPhoneNumberId: botPhone,
            PhoneNumber: userPhone,
            LanguageCode: "es",
            OriginalText: "¿Me recuerdas?"
        );

        // Act
        await fixture.Host.InvokeMessageAndWaitAsync(incomingEvent);

        // Assert
        var streamEvents = await session.Events.FetchStreamAsync(userPhone);
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