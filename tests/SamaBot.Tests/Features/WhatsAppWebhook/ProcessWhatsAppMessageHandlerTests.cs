using AwesomeAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using SamaBot.Api.Core.Events;
using SamaBot.Api.Features.Tenancy; // Añadido para TenantProfile
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
        var tenantSlug = "test-tenant-handler-1";
        var botPhoneId = "12345";

        await SeedTenantAsync(tenantSlug, botPhoneId);

        var command = new ProcessWhatsAppMessage(
            MessageId: "wamid.HANDLER",
            BotPhoneNumberId: botPhoneId, // El comando viene con el ID de Meta
            PhoneNumber: "34999111222",
            Text: "Pure Handler Text",
            Timestamp: DateTimeOffset.UtcNow,
            RawPayload: "{}"
        );

        // Act: Directly invoke the message bypassing the HTTP layer
        await fixture.Host.InvokeMessageAndWaitAsync(command);

        // Assert: The message was correctly sourced in the database
        using var session = fixture.Host.Services.GetRequiredService<IDocumentStore>().LightweightSession(tenantSlug);

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
        var tenantSlug = "test-tenant-handler-2";
        var botPhoneId = "123";

        await SeedTenantAsync(tenantSlug, botPhoneId);

        var command = new ProcessWhatsAppMessage("wamid.DUP", botPhoneId, "34999111222", "Texto", DateTimeOffset.UtcNow, "{}");

        // Act: Send the same message twice to simulate Meta webhook retries
        await fixture.Host.InvokeMessageAndWaitAsync(command);
        await fixture.Host.InvokeMessageAndWaitAsync(command);

        // Assert: It should only exist once in the stream
        using var session = fixture.Host.Services.GetRequiredService<IDocumentStore>().LightweightSession(tenantSlug);

        var streamEvents = await session.Events.FetchStreamAsync("34999111222");

        var receivedCount = streamEvents.Count(e => e.Data is MessageReceived mr && mr.MessageId == "wamid.DUP");
        receivedCount.Should().Be(1);
    }

    private async Task SeedTenantAsync(string slug, string botPhoneId)
    {
        using var session = fixture.Host.Services.GetRequiredService<IDocumentStore>().LightweightSession();

        if (await session.LoadAsync<TenantProfile>(slug) == null)
        {
            session.Store(new TenantProfile
            {
                Id = slug,
                BotPhoneNumberId = botPhoneId
            });
            await session.SaveChangesAsync();
        }
    }
}