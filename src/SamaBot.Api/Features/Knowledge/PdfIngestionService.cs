using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace SamaBot.Api.Features.Knowledge;

public interface IPdfIngestionService
{
    Task IngestPdfStreamAsync(Stream pdfStream, string fileName, CancellationToken ct = default);
}

public class PdfIngestionService(
    IKnowledgeBaseService knowledgeBaseService,
    ILogger<PdfIngestionService> logger) : IPdfIngestionService
{
    public async Task IngestPdfStreamAsync(Stream pdfStream, string fileName, CancellationToken ct = default)
    {
        using var document = PdfDocument.Open(pdfStream);
        var textBuilder = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            textBuilder.AppendLine(ContentOrderTextExtractor.GetText(page));
        }

        var chunks = ChunkText(textBuilder.ToString(), 1000, 200);

        await knowledgeBaseService.IngestChunksAsync(chunks, fileName, ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Successfully ingested {ChunkCount} chunks from {FileName}", chunks.Count, fileName);
        }
    }

    private static List<string> ChunkText(string text, int chunkSize, int overlap)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var chunks = new List<string>();
        var position = 0;

        while (position < text.Length)
        {
            var length = Math.Min(chunkSize, text.Length - position);
            chunks.Add(text.Substring(position, length));
            position += chunkSize - overlap;
        }

        return chunks;
    }
}