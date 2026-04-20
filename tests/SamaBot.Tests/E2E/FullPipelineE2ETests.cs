using Alba;
using AwesomeAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using SamaBot.Api.Core.Events;
using SamaBot.Api.Features.WhatsAppWebhook;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Wolverine.Tracking;

namespace SamaBot.Tests.E2E;

[Collection("Integration")]
public class FullPipelineE2ETests(IntegrationAppFixture fixture)
{
    [Fact]
    public async Task FullRAGJourney_FromIngestionToAIResponse()
    {
        // --- 1. Arrange: Data Ingestion (MULTIPART UPLOAD) ---
        var tempPdfPath = Path.Combine(Path.GetTempPath(), $"test_knowledge_{Guid.NewGuid()}.pdf");
        var secretInfo = "The secret access code for SamaBot is 998877.";
        CreateTestPdf(tempPdfPath, secretInfo);

        await fixture.Host.Scenario(s =>
        {
            s.Post.Url("/api/admin/ingest");

            var fileContent = new ByteArrayContent(File.ReadAllBytes(tempPdfPath));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

            var multipart = new MultipartFormDataContent
            {
                { fileContent, "file", Path.GetFileName(tempPdfPath) }
            };

            var stream = multipart.ReadAsStream();
            s.ConfigureHttpContext(context =>
            {
                context.Request.ContentType = multipart.Headers.ContentType?.ToString();
                context.Request.ContentLength = stream.Length;
                context.Request.Body = stream;
            });

            s.StatusCodeShouldBeOk();
        });

        // --- 2. Arrange: Webhook Payload ---
        var testPhoneNumber = $"34999000{Random.Shared.Next(100, 999)}";
        var payload = $$"""
        {
          "object": "whatsapp_business_account",
          "entry": [ { "changes": [ { "value": {
            "metadata": { "phone_number_id": "12345" },
            "messages": [ { "from": "{{testPhoneNumber}}", "id": "wamid.RAG_TEST", "timestamp": "1603059201", "text": { "body": "What is the secret code?" }, "type": "text" } ]
          } } ] } ]
        }
        """;

        var signature = GenerateSignature(payload, "integration_test_secret");

        // --- 3. Act: The Webhook Cascade ---
        await fixture.Host.ExecuteAndWaitAsync(async () =>
        {
            await fixture.Host.Scenario(s =>
            {
                s.Post.Text(payload).ContentType("application/json").ToUrl("/api/whatsapp/webhook");
                s.WithRequestHeader("X-Hub-Signature-256", signature);
                s.StatusCodeShouldBeOk();
            });
        });

        // --- 4. Assert: Verification of the Event Stream Cascade ---
        using var session = fixture.Host.Services.GetRequiredService<IDocumentStore>().LightweightSession();
        var streamEvents = await session.Events.FetchStreamAsync(testPhoneNumber);

        streamEvents.Should().NotBeEmpty("The event stream should have been populated by the webhook.");

        var received = streamEvents.Select(e => e.Data).OfType<MessageReceived>().FirstOrDefault();
        received.Should().NotBeNull("Phase 1: MessageReceived event is missing.");
        received!.Text.Should().Be("What is the secret code?");

        var analyzed = streamEvents.Select(e => e.Data).OfType<MessageAnalyzed>().FirstOrDefault();
        analyzed.Should().NotBeNull("Phase 2: MessageAnalyzed event is missing.");
        analyzed!.LanguageCode.Should().Be("es");

        var reply = streamEvents.Select(e => e.Data).OfType<ReplyGenerated>().FirstOrDefault();
        reply.Should().NotBeNull("Phase 3: ReplyGenerated event is missing.");
        reply!.ReplyText.Should().NotBeNullOrWhiteSpace();

        // Cleanup
        if (File.Exists(tempPdfPath)) File.Delete(tempPdfPath);
    }

    [Fact]
    public async Task Webhook_Idempotency_DuplicateMessages_AreIgnored()
    {
        // --- 1. Arrange: Webhook Payload ---
        var testPhoneNumber = $"34999111{Random.Shared.Next(100, 999)}";
        var messageId = $"wamid.IDEMP_{Guid.NewGuid():N}";

        var payload = $$"""
        {
          "object": "whatsapp_business_account",
          "entry": [ { "changes": [ { "value": {
            "metadata": { "phone_number_id": "12345" },
            "messages": [ { "from": "{{testPhoneNumber}}", "id": "{{messageId}}", "timestamp": "1603059201", "text": { "body": "Idempotency test!" }, "type": "text" } ]
          } } ] } ]
        }
        """;

        var signature = GenerateSignature(payload, "integration_test_secret");

        // --- 2. Act: Send the payload TWICE ---

        // First delivery (Should be processed)
        await fixture.Host.Scenario(s =>
        {
            s.Post.Text(payload).ContentType("application/json").ToUrl("/api/whatsapp/webhook");
            s.WithRequestHeader("X-Hub-Signature-256", signature);
            s.StatusCodeShouldBeOk();
        });

        // Second delivery - Meta retry (Should be ignored by our Idempotency check)
        await fixture.Host.Scenario(s =>
        {
            s.Post.Text(payload).ContentType("application/json").ToUrl("/api/whatsapp/webhook");
            s.WithRequestHeader("X-Hub-Signature-256", signature);
            s.StatusCodeShouldBeOk();
        });

        // --- 3. Assert: Verification of Idempotency ---
        using var session = fixture.Host.Services.GetRequiredService<IDocumentStore>().LightweightSession();

        // 3.1. Verify the Stream: There should be exactly ONE MessageReceived event for this messageId
        var streamEvents = await session.Events.FetchStreamAsync(testPhoneNumber);
        var receivedEvents = streamEvents.Select(e => e.Data).OfType<MessageReceived>().Where(x => x.MessageId == messageId).ToList();

        receivedEvents.Should().HaveCount(1, "The idempotency check should have intercepted and ignored the second webhook call.");

        // 3.2. Verify the Projection: The ProcessedMessage document should exist in the database
        var processedMessage = await session.LoadAsync<ProcessedMessage>(messageId);
        processedMessage.Should().NotBeNull("The EventProjection should have created a ProcessedMessage document during the first processing.");
        processedMessage!.Id.Should().Be(messageId);
    }

    private static string GenerateSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static void CreateTestPdf(string path, string content)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        builder.AddPage(595, 842).AddText(content, 10, new PdfPoint(25, 800), font);
        File.WriteAllBytes(path, builder.Build());
    }
}