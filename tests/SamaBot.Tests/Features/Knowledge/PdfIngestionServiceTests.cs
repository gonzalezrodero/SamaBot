using Moq;
using Moq.AutoMock;
using SamaBot.Api.Features.Knowledge;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace SamaBot.Tests.Features.Knowledge;

public class PdfIngestionServiceTests
{
    private readonly AutoMocker mocker;
    private readonly PdfIngestionService sut;

    public PdfIngestionServiceTests()
    {
        mocker = new AutoMocker();
        sut = mocker.CreateInstance<PdfIngestionService>();
    }

    [Fact]
    public async Task IngestPdfStreamAsync_MultiplePages_CombinesTextCorrectly()
    {
        // Arrange
        var pages = new[] { "Page 1 content", "Page 2 content" };
        using var pdfStream = CreateTestPdfStream(pages);

        // Act
        await sut.IngestPdfStreamAsync(pdfStream, "test_document.pdf", CancellationToken.None);

        // Assert
        mocker.GetMock<IKnowledgeBaseService>().Verify(k =>
            k.IngestChunksAsync(
                It.Is<IEnumerable<string>>(chunks =>
                    chunks.Any(c => c.Contains("Page 1")) && chunks.Any(c => c.Contains("Page 2"))),
                "test_document.pdf",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IngestPdfStreamAsync_LongText_CreatesOverlappingChunks()
    {
        // Arrange
        // 1200 chars to trigger chunking (chunkSize: 1000, overlap: 200)
        var longText = new string('A', 1200);
        using var pdfStream = CreateTestPdfStream([longText]);

        // Act
        await sut.IngestPdfStreamAsync(pdfStream, "long_document.pdf", CancellationToken.None);

        // Assert
        mocker.GetMock<IKnowledgeBaseService>().Verify(k =>
            k.IngestChunksAsync(
                It.Is<IEnumerable<string>>(chunks => chunks.Count() == 2),
                "long_document.pdf",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Helper method to create an in-memory PDF stream instead of writing to disk
    private static MemoryStream CreateTestPdfStream(string[] contents)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        foreach (var text in contents)
        {
            // Standard A4: 595 x 842 points
            var page = builder.AddPage(595, 842);
            page.AddText(text, 10, new PdfPoint(25, 800), font);
        }

        var stream = new MemoryStream(builder.Build())
        {
            Position = 0
        };
        return stream;
    }
}