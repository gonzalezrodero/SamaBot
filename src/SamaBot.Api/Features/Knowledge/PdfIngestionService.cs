using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace SamaBot.Api.Features.Knowledge;

public interface IPdfIngestionService
{
    Task IngestPdfAsync(string filePath, CancellationToken ct = default);
}

public class PdfIngestionService(
    IKnowledgeBaseService knowledgeBaseService,
    ILogger<PdfIngestionService> logger) : IPdfIngestionService
{
    public async Task IngestPdfAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            logger.LogError("File not found at path: {FilePath}", filePath);
            throw new FileNotFoundException("PDF file not found.", filePath);
        }

        var fileName = Path.GetFileName(filePath);
        logger.LogInformation("Starting text extraction for document: {FileName}", fileName);

        using var document = PdfDocument.Open(filePath);
        var fullText = string.Empty;

        foreach (var page in document.GetPages())
        {
            var pageText = ContentOrderTextExtractor.GetText(page);
            fullText += pageText + "\n\n";
        }

        var chunks = ChunkText(fullText, chunkSize: 1000, overlap: 200);
        logger.LogInformation("Extracted {ChunkCount} chunks from {FileName}. Starting vector generation...", chunks.Count, fileName);

        await knowledgeBaseService.IngestChunksAsync(chunks, fileName, ct);

        logger.LogInformation("Successfully ingested {FileName} into the Knowledge Base.", fileName);
    }

    /// <summary>
    /// Splits a large string into smaller chunks with a specified overlap to maintain context boundaries.
    /// </summary>
    private static List<string> ChunkText(string text, int chunkSize, int overlap)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var chunks = new List<string>();
        var position = 0;

        while (position < text.Length)
        {
            var length = Math.Min(chunkSize, text.Length - position);
            var chunk = text.Substring(position, length);
            chunks.Add(chunk);

            // Move forward, subtracting the overlap so the next chunk shares some context
            position += chunkSize - overlap;
        }

        return chunks;
    }
}