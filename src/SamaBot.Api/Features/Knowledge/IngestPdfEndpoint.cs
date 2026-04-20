using Wolverine.Http;

namespace SamaBot.Api.Features.Knowledge;

public class IngestPdfEndpoint
{
    [WolverinePost("/api/admin/ingest")]
    public async Task<IResult> Ingest(
        IFormFile file,
        IPdfIngestionService ingestionService,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            return Results.BadRequest(new { Error = "No file uploaded." });
        }

        if (file.ContentType != "application/pdf")
        {
            return Results.BadRequest(new { Error = "Only PDF files are supported." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            await ingestionService.IngestPdfStreamAsync(stream, file.FileName, ct);

            return Results.Ok(new { Message = $"Successfully ingested {file.FileName} into the vector database." });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, title: "Ingestion failed");
        }
    }
}