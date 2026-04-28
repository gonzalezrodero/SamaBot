using Alba;
using AwesomeAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using SamaBot.Api.Core.Events;
using SamaBot.Api.Features.Tenancy;
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
        var tenant = $"club-{Guid.NewGuid():N}";
        var botPhoneId = $"bot-{Guid.NewGuid():N}";
        await SeedTenantAsync(tenant, botPhoneId);

        // --- 1. Arrange: Data Ingestion ---
        var tempPdfPath = Path.Combine(Path.GetTempPath(), $"test_knowledge_{Guid.NewGuid()}.pdf");
        var secretInfo = "The secret access code for SamaBot is 998877.";
        CreateTestPdf(tempPdfPath, secretInfo);

        await fixture.Host.Scenario(s =>
        {
            s.Post.Url($"/api/admin/ingest/{tenant}");

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
        var testPhoneNumber = $"{Random.Shared.Next(600000000, 699999999)}";
        var payload = $$"""
        {
          "object": "whatsapp_business_account",
          "entry": [ { "changes": [ { "value": {
            "metadata": { "phone_number_id": "{{botPhoneId}}" },
            "messages": [ { "from": "{{testPhoneNumber}}", "id": "wamid.RAG_TEST", "timestamp": "1603059201", "text": { "body": "What is the secret code?" }, "type": "text" } ]
          } } ] } ]
        }
        """;

        var signature = GenerateSignature(payload, "integration_test_secret");

        // --- 3. Act ---
        await fixture.Host.ExecuteAndWaitAsync(async () =>
        {
            await fixture.Host.Scenario(s =>
            {
                s.Post.Text(payload).ContentType("application/json").ToUrl("/api/whatsapp/webhook");
                s.WithRequestHeader("X-Hub-Signature-256", signature);
                s.StatusCodeShouldBeOk();
            });
        });

        // --- 4. Assert ---
        using var session = fixture.Host.Services.GetRequiredService<IDocumentStore>().LightweightSession(tenant);
        var streamEvents = await session.Events.FetchStreamAsync(testPhoneNumber);

        streamEvents.Should().NotBeEmpty("The event stream should have been populated by the webhook.");

        var received = streamEvents.Select(e => e.Data).OfType<MessageReceived>().FirstOrDefault();
        received.Should().NotBeNull();
        received!.TenantId.Should().Be(tenant);

        var reply = streamEvents.Select(e => e.Data).OfType<ReplyGenerated>().FirstOrDefault();
        reply.Should().NotBeNull();
        reply!.Text.Should().Contain("998877");

        if (File.Exists(tempPdfPath)) File.Delete(tempPdfPath);
    }

    [Fact]
    public async Task Webhook_Idempotency_DuplicateMessages_AreIgnored()
    {
        var tenantSlug = $"club-idemp-{Guid.NewGuid():N}";
        var botPhoneId = $"bot-idemp-{Guid.NewGuid():N}";
        await SeedTenantAsync(tenantSlug, botPhoneId);

        var testPhoneNumber = $"{Random.Shared.Next(700000000, 799999999)}";
        var messageId = $"wamid.IDEMP_{Guid.NewGuid():N}";

        var payload = $$"""
        {
          "object": "whatsapp_business_account",
          "entry": [ { "changes": [ { "value": {
            "metadata": { "phone_number_id": "{{botPhoneId}}" },
            "messages": [ { "from": "{{testPhoneNumber}}", "id": "{{messageId}}", "timestamp": "1603059201", "text": { "body": "Idempotency test!" }, "type": "text" } ]
          } } ] } ]
        }
        """;

        var signature = GenerateSignature(payload, "integration_test_secret");

        // Act & Assert
        await fixture.Host.Scenario(s =>
        {
            s.Post.Text(payload).ContentType("application/json").ToUrl("/api/whatsapp/webhook");
            s.WithRequestHeader("X-Hub-Signature-256", signature);
            s.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(s =>
        {
            s.Post.Text(payload).ContentType("application/json").ToUrl("/api/whatsapp/webhook");
            s.WithRequestHeader("X-Hub-Signature-256", signature);
            s.StatusCodeShouldBeOk();
        });

        using var session = fixture.Host.Services.GetRequiredService<IDocumentStore>().LightweightSession(tenantSlug);
        var streamEvents = await session.Events.FetchStreamAsync(testPhoneNumber);
        streamEvents.Select(e => e.Data).OfType<MessageReceived>().Count(x => x.MessageId == messageId).Should().Be(1);
    }

    private async Task SeedTenantAsync(string tenant, string botPhoneId)
    {
        using var session = fixture.Host.Services.GetRequiredService<IDocumentStore>().LightweightSession();
        session.Store(new TenantProfile { Id = tenant, BotPhoneNumberId = botPhoneId });
        await session.SaveChangesAsync();
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