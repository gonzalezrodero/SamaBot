using Marten;
using SamaBot.Api.Features.Knowledge.Extractors;
using SamaBot.Api.Features.Knowledge.Services;
using SamaBot.Api.Features.Tenancy;
using Wolverine.Http;

namespace SamaBot.Api.Features.Knowledge;

public class IngestEndpoint
{
    [WolverinePost("/api/admin/ingest/{tenantId}")]
    public async Task<IResult> Ingest(
        string tenantId,
        IFormFile file,
        IDocumentSession session,
        IEnumerable<IDocumentExtractor> extractors,
        IKnowledgeIngestionService ingestionService,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            return Results.BadRequest(new { Error = "No file uploaded." });
        }

        // 1. Validate that the tenant exists in our registry
        var tenant = await session.LoadAsync<TenantProfile>(tenantId, ct);
        if (tenant == null)
        {
            return Results.BadRequest(new { Error = $"Tenant '{tenantId}' is not registered." });
        }

        // 2. Get the extension to find the right strategy
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var extractor = extractors.FirstOrDefault(e => e.SupportedExtensions.Contains(extension));

        // 3. If no extractor supports this extension, reject the request
        if (extractor == null)
        {
            return Results.BadRequest(new { Error = $"File type '{extension}' is not supported. Please upload .pdf, .md, or .txt." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            string extractedText = await extractor.ExtractTextAsync(stream);

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return Results.BadRequest(new { Error = "The uploaded file contains no readable text." });
            }

            await ingestionService.IngestDocumentAsync(tenantId, extractedText, file.FileName, ct);

            return Results.Ok(new { Message = $"Successfully ingested {file.FileName} into the vector database." });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, title: "Ingestion failed");
        }
    }
}