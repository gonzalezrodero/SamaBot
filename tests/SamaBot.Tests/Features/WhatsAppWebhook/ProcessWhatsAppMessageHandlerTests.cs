using AwesomeAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using SamaBot.Api.Core.Events;
using SamaBot.Api.Features.WhatsAppWebhook;
using SamaBot.Tests.Extensions;

namespace SamaBot.Tests.Features.WhatsAppWebhook;

[Collection("Integration")]
public class ProcessWhatsAppMessageHandlerTests(IntegrationAppFixture fixture)
{
    [Fact]
    public async Task GivenValidMessageCommand_WhenHandlerExecutes_ThenItAppendsToMartenEventStore()
    {
        // Arrange
        var tenantId = "12345";
        var command = new ProcessWhatsAppMessage(
            MessageId: "wamid.HANDLER",
            BotPhoneNumberId: tenantId,
            PhoneNumber: "34999111222",
            Text: "Pure Handler Text",
            Timestamp: DateTimeOffset.UtcNow,
            RawPayload: "{}"
        );

        // Act: Directly invoke the message bypassing the HTTP layer
        await fixture.Host.InvokeMessageAndWaitAsync(command);

        // Assert: The message was correctly sourced in the database
        using var session = fixture.Host.Services.GetRequiredService<IDocumentStore>().LightweightSession(tenantId);

        var streamEvents = await session.Events.FetchStreamAsync("34999111222");

        streamEvents.Should().NotBeEmpty();

        var messageReceived = streamEvents.FirstOrDefault(e => e.Data is MessageReceived)?.Data as MessageReceived;
        messageReceived.Should().NotBeNull();
        messageReceived!.MessageId.Should().Be("wamid.HANDLER");
        messageReceived.Text.Should().Be("Pure Handler Text");
    }

    [Fact]
    public async Task GivenDuplicateMessageId_WhenHandlerExecutes_ThenItIgnoresSilentlyForIdempotency()
    {
        // Arrange
        var tenantId = "123";
        var command = new ProcessWhatsAppMessage("wamid.DUP", tenantId, "34999111222", "Texto", DateTimeOffset.UtcNow, "{}");

        // Act: Send the same message twice to simulate Meta webhook retries
        await fixture.Host.InvokeMessageAndWaitAsync(command);
        await fixture.Host.InvokeMessageAndWaitAsync(command);

        // Assert: It should only exist once in the stream
        using var session = fixture.Host.Services.GetRequiredService<IDocumentStore>().LightweightSession(tenantId);

        var streamEvents = await session.Events.FetchStreamAsync("34999111222");

        var receivedCount = streamEvents.Count(e => e.Data is MessageReceived mr && mr.MessageId == "wamid.DUP");
        receivedCount.Should().Be(1);
    }
}