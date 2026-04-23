using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace SamaBot.Api.Features.Knowledge;

public interface IPdfIngestionService
{
    Task IngestPdfStreamAsync(string tenantId, Stream pdfStream, string fileName, CancellationToken ct = default);
}

public class PdfIngestionService(
    IKnowledgeBaseService knowledgeBaseService,
    ILogger<PdfIngestionService> logger) : IPdfIngestionService
{
    public async Task IngestPdfStreamAsync(string tenantId, Stream pdfStream, string fileName, CancellationToken ct = default)
    {
        using var document = PdfDocument.Open(pdfStream);
        var textBuilder = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            textBuilder.AppendLine(ContentOrderTextExtractor.GetText(page));
        }

        var chunks = ChunkText(textBuilder.ToString(), 1000, 200);
        await knowledgeBaseService.ClearTenantChunksAsync(tenantId, ct);

        // Pass the tenantId to the ingestion method
        await knowledgeBaseService.IngestChunksAsync(tenantId, chunks, fileName, ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Successfully ingested {ChunkCount} chunks for tenant {TenantId} from {FileName}", chunks.Count, tenantId, fileName);
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