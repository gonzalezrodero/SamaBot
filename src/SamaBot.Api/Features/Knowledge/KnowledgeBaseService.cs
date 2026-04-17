using Marten;
using Microsoft.Extensions.AI;
using SamaBot.Api.Core.Entities;

namespace SamaBot.Api.Features.Knowledge;

public interface IKnowledgeBaseService
{
    Task<IReadOnlyList<DocumentChunk>> SearchAsync(string query, int limit = 3, CancellationToken ct = default);
    Task IngestChunksAsync(IEnumerable<string> contents, string source, CancellationToken ct = default);
}

public class KnowledgeBaseService(
    IDocumentSession session,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    : IKnowledgeBaseService
{
    public async Task<IReadOnlyList<DocumentChunk>> SearchAsync(
            string query, int limit = 3, CancellationToken ct = default)
    {
        var result = await embeddingGenerator.GenerateAsync([query], cancellationToken: ct);
        var searchVector = result[0].Vector.ToArray();

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

        var embeddings = await embeddingGenerator.GenerateAsync(validChunks, cancellationToken: ct);

        for (int i = 0; i < validChunks.Count; i++)
        {
            var chunk = new DocumentChunk(
                Guid.NewGuid(),
                validChunks[i],
                source,
                embeddings[i].Vector.ToArray(),
                DateTimeOffset.UtcNow);

            session.Store(chunk);
        }

        await session.SaveChangesAsync(ct);
    }
}