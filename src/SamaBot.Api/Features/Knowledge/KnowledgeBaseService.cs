using Marten;
using SamaBot.Api.Core.Entities;

namespace SamaBot.Api.Features.Knowledge;

public interface IKnowledgeBaseService
{
    Task<IReadOnlyList<DocumentChunk>> SearchAsync(string query, int limit = 3, CancellationToken ct = default);
    Task IngestChunksAsync(IEnumerable<string> contents, string source, CancellationToken ct = default);
}

public class KnowledgeBaseService(IDocumentSession session, IEmbeddingService embeddingService, ILogger<KnowledgeBaseService> logger) : IKnowledgeBaseService
{
    public async Task<IReadOnlyList<DocumentChunk>> SearchAsync(string query, int limit = 3, CancellationToken ct = default)
    {
        logger.LogDebug("SearchAsync: query length {Len} limit {Limit}", query?.Length ?? 0, limit);

        var searchVector = await embeddingService.GenerateEmbeddingAsync(query, ct);

        var sql = @"
            SELECT data FROM mt_doc_documentchunk 
            ORDER BY public.extract_embedding(data) <=> CAST(? AS vector) 
            LIMIT ?";

        logger.LogDebug("Executing vector search SQL with limit {Limit}", limit);
        var results = await session.QueryAsync<DocumentChunk>(sql, ct, searchVector, limit) ?? [];
        logger.LogInformation("SearchAsync returned {Count} chunks", results?.Count ?? 0);

        return results;
    }

    public async Task IngestChunksAsync(IEnumerable<string> contents, string source, CancellationToken ct = default)
    {
        var validChunks = contents.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        logger.LogDebug("IngestChunksAsync: valid chunk count {Count} source {Source}", validChunks.Count, source);

        if (validChunks.Count == 0) return;

        var embeddingTasks = validChunks.Select(chunk => embeddingService.GenerateEmbeddingAsync(chunk, ct));
        var embeddings = await Task.WhenAll(embeddingTasks);

        for (int i = 0; i < validChunks.Count; i++)
        {
            var chunk = new DocumentChunk(
                Guid.NewGuid(),
                validChunks[i],
                source,
                embeddings[i],
                DateTimeOffset.UtcNow);

            session.Store(chunk);
        }

        await session.SaveChangesAsync(ct);
        logger.LogInformation("IngestChunksAsync: stored {Count} chunks for source {Source}", validChunks.Count, source);
    }
}