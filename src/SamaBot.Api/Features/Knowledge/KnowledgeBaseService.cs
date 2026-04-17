using Marten;
using SamaBot.Api.Core.Entities;

namespace SamaBot.Api.Features.Knowledge;

public interface IKnowledgeBaseService
{
    Task<IReadOnlyList<DocumentChunk>> SearchAsync(string query, int limit = 3, CancellationToken ct = default);
    Task IngestChunksAsync(IEnumerable<string> contents, string source, CancellationToken ct = default);
}

public class KnowledgeBaseService(
    IDocumentSession session,
    IEmbeddingService embeddingService)
    : IKnowledgeBaseService
{
    public async Task<IReadOnlyList<DocumentChunk>> SearchAsync(
            string query, int limit = 3, CancellationToken ct = default)
    {
        var searchVector = await embeddingService.GenerateEmbeddingAsync(query, ct);

        var sql = @"
            SELECT data FROM mt_doc_documentchunk 
            ORDER BY public.extract_embedding(data) <=> CAST(? AS vector) 
            LIMIT ?";

        return [.. await session.QueryAsync<DocumentChunk>(sql, ct, searchVector, limit)];
    }

    public async Task IngestChunksAsync(
        IEnumerable<string> contents, string source, CancellationToken ct = default)
    {
        var validChunks = contents.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
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
    }
}