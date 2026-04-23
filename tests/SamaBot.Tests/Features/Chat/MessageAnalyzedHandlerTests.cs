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
        var tenantId = "club-sama";
        var botPhone = "34111222333";
        var userPhone = "34888777666";

        var incomingEvent = new MessageAnalyzed(
            MessageId: "atomic.Chat1",
            BotPhoneNumberId: botPhone,
            PhoneNumber: userPhone,
            LanguageCode: "ca",
            OriginalText: "Quina és la contrasenya?",
            TenantId: tenantId
        );

        // Act
        await fixture.Host.InvokeMessageAndWaitAsync(incomingEvent);

        // Assert
        using var session = fixture.Host.Services.GetRequiredService<IDocumentStore>().LightweightSession(tenantId);
        var streamEvents = await session.Events.FetchStreamAsync(userPhone);

        var replyGenerated = streamEvents.FirstOrDefault(e => e.Data is ReplyGenerated)?.Data as ReplyGenerated;

        replyGenerated.Should().NotBeNull("The handler should have appended a ReplyGenerated event to the stream.");

        replyGenerated!.Text.Should().Be("Mocked AI Response: Soy SamaBot y esto es un test E2E.");
        replyGenerated.MessageId.Should().Be("atomic.Chat1");
        replyGenerated.BotPhoneNumberId.Should().Be(botPhone);
        replyGenerated.PhoneNumber.Should().Be(userPhone);
        replyGenerated.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task GivenPreviousConversation_WhenHandlerRuns_ThenItUsesHistoryAndAppendsReply()
    {
        // Arrange
        var tenantId = "club-sama";
        var botPhone = "34111222333";
        var userPhone = "34999555111";

        using var session = fixture.Host.Services.GetRequiredService<IDocumentStore>().LightweightSession(tenantId);

        // 1. Pre-populate the Marten Event Store
        session.Events.Append(userPhone, new MessageReceived("old.1", userPhone, "Hola", tenantId, botPhone, DateTimeOffset.UtcNow));
        session.Events.Append(userPhone, new ReplyGenerated("old.1", botPhone, userPhone, "¡Hola! Soy SamàBot.", tenantId));
        session.Events.Append(userPhone, new MessageReceived("atomic.Chat2", userPhone, "¿Me recuerdas?", tenantId, botPhone, DateTimeOffset.UtcNow));
        await session.SaveChangesAsync();

        // 2. The new incoming message
        var incomingEvent = new MessageAnalyzed(
            MessageId: "atomic.Chat2",
            BotPhoneNumberId: botPhone,
            PhoneNumber: userPhone,
            LanguageCode: "es",
            OriginalText: "¿Me recuerdas?",
            TenantId: tenantId
        );

        // Act
        await fixture.Host.InvokeMessageAndWaitAsync(incomingEvent);

        // Assert
        var streamEvents = await session.Events.FetchStreamAsync(userPhone);
        var replies = streamEvents.Select(e => e.Data).OfType<ReplyGenerated>().ToList();

        replies.Should().HaveCount(2);
        replies.Last().MessageId.Should().Be("atomic.Chat2");

        fixture.BedrockClientMock.Verify(c => c.InvokeModelAsync(It.Is<InvokeModelRequest>(req =>
            VerifyChatHistoryPayload(req)
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static bool VerifyChatHistoryPayload(InvokeModelRequest request)
    {
        var requestJson = System.Text.Encoding.UTF8.GetString(request.Body.ToArray());
        return requestJson.Contains("Hola") &&
               requestJson.Contains("¡Hola! Soy SamàBot.") &&
               requestJson.Contains("¿Me recuerdas?");
    }
}