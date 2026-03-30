using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace SamaBot.Api.Features.Knowledge;

/// <summary>
/// Request model for triggering PDF ingestion.
/// </summary>
public record IngestPdfRequest(string FilePath);

public class IngestPdfEndpoint
{
    /// <summary>
    /// Admin endpoint to trigger the ingestion of a local PDF file into the Knowledge Base.
    /// </summary>
    [WolverinePost("/api/admin/ingest")]
    public async Task<IResult> Ingest(
        [FromBody] IngestPdfRequest request,
        IPdfIngestionService ingestionService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return Results.BadRequest(new { Error = "FilePath is required." });
        }

        try
        {
            await ingestionService.IngestPdfAsync(request.FilePath, ct);

            return Results.Ok(new { Message = $"Successfully ingested {request.FilePath} into the vector database." });
        }
        catch (FileNotFoundException ex)
        {
            return Results.NotFound(new { Error = ex.Message, Path = request.FilePath });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, title: "Ingestion failed");
        }
    }
}