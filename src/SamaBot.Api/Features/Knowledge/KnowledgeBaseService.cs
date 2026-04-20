using Marten;
using SamaBot.Api.Core.Entities;
using System.Collections.Concurrent;

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

        var chunksToStore = new ConcurrentBag<DocumentChunk>();

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = 5,
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(validChunks, options, async (chunkText, token) =>
        {
            var embedding = await embeddingService.GenerateEmbeddingAsync(chunkText, token);

            var chunk = new DocumentChunk(
                Guid.NewGuid(),
                chunkText,
                source,
                embedding,
                DateTimeOffset.UtcNow);

            chunksToStore.Add(chunk);
        });

        session.Store(chunksToStore.ToArray());
        await session.SaveChangesAsync(ct);
    }
}