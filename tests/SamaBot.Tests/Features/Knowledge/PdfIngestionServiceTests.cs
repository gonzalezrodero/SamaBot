using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using SamaBot.Api.Features.Knowledge;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace SamaBot.Tests.Features.Knowledge;

public class PdfIngestionServiceTests : IDisposable
{
    private readonly AutoMocker mocker;
    private readonly PdfIngestionService sut;
    private readonly string tempFilePath;

    public PdfIngestionServiceTests()
    {
        mocker = new AutoMocker();
        sut = mocker.CreateInstance<PdfIngestionService>();
        tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");
    }

    [Fact]
    public async Task IngestPdfAsync_MultiplePages_CombinesTextCorrectly()
    {
        // Arrange
        var pages = new[] { "Page 1 content", "Page 2 content" };
        CreateTestPdf(tempFilePath, pages);

        // Act
        await sut.IngestPdfAsync(tempFilePath);

        // Assert
        mocker.GetMock<IKnowledgeBaseService>().Verify(k =>
            k.IngestChunksAsync(
                It.Is<IEnumerable<string>>(chunks =>
                    chunks.Any(c => c.Contains("Page 1")) && chunks.Any(c => c.Contains("Page 2"))),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IngestPdfAsync_LongText_CreatesOverlappingChunks()
    {
        // Arrange
        // 1200 chars to trigger chunking (chunkSize: 1000, overlap: 200)
        var longText = new string('A', 1200);
        CreateTestPdf(tempFilePath, [longText]);

        // Act
        await sut.IngestPdfAsync(tempFilePath);

        // Assert
        mocker.GetMock<IKnowledgeBaseService>().Verify(k =>
            k.IngestChunksAsync(
                It.Is<IEnumerable<string>>(chunks => chunks.Count() == 2),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IngestPdfAsync_FileNotFound_ThrowsAndLogs()
    {
        var act = () => sut.IngestPdfAsync("missing.pdf");

        await act.Should().ThrowAsync<FileNotFoundException>();

        mocker.GetMock<ILogger<PdfIngestionService>>().Verify(l =>
            l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static void CreateTestPdf(string path, string[] contents)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        foreach (var text in contents)
        {
            // Standard A4: 595 x 842 points
            var page = builder.AddPage(595, 842);
            page.AddText(text, 10, new PdfPoint(25, 800), font);
        }

        File.WriteAllBytes(path, builder.Build());
    }

    public void Dispose()
    {
        if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
        GC.SuppressFinalize(this);
    }
}