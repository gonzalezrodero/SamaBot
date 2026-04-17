using Alba;
using AwesomeAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SamaBot.Api.Core.Entities;
using SamaBot.Api.Features.Knowledge;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace SamaBot.Tests.E2E;

[Collection("Integration")]
public class IngestPdfEndpointTests(IntegrationAppFixture fixture) : IDisposable
{
    private readonly string _tempPdfPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");

    [Fact]
    public async Task Post_Ingest_ValidFile_ReturnsOk_AndStoresChunksInMarten()
    {
        // Arrange
        CreateSimplePdf(_tempPdfPath, "Integration test content for RAG.");
        var request = new IngestPdfRequest(_tempPdfPath);

        fixture.EmbeddingMock.Setup(x => x.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[512]);

        // Act
        await fixture.Host.Scenario(s =>
        {
            s.Post.Json(request).ToUrl("/api/admin/ingest");
            s.StatusCodeShouldBeOk();
        });

        // Assert
        using var session = fixture.Host.Services.GetRequiredService<IDocumentStore>().LightweightSession();
        var chunks = await session.Query<DocumentChunk>()
            .Where(x => x.SourceDocument == Path.GetFileName(_tempPdfPath))
            .ToListAsync();

        chunks.Should().NotBeEmpty();
    }

    private static void CreateSimplePdf(string path, string content)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(595, 842);
        page.AddText(content, 10, new PdfPoint(25, 800), font);

        File.WriteAllBytes(path, builder.Build());
    }

    public void Dispose()
    {
        if (File.Exists(_tempPdfPath)) File.Delete(_tempPdfPath);
        GC.SuppressFinalize(this);
    }
}