using AwesomeAssertions;
using Marten;
using Microsoft.Extensions.AI;
using Moq;
using Moq.AutoMock;
using SamaBot.Api.Core.Entities;
using SamaBot.Api.Features.Knowledge;

namespace SamaBot.Tests.Features.Knowledge;

public class KnowledgeBaseServiceTests
{
    private readonly AutoMocker mocker;
    private readonly KnowledgeBaseService sut;

    public KnowledgeBaseServiceTests()
    {
        mocker = new AutoMocker();
        sut = mocker.CreateInstance<KnowledgeBaseService>();
    }

    [Fact]
    public async Task SearchAsync_GeneratesEmbeddingAndQueriesDatabase_ReturnsChunks()
    {
        // Arrange
        var query = "Test query";
        var mockVector = new float[] { 0.1f, 0.2f, 0.3f };

        var mockGeneratedEmbedding = new GeneratedEmbeddings<Embedding<float>>(
            [new Embedding<float>(mockVector)]);

        mocker.GetMock<IEmbeddingGenerator<string, Embedding<float>>>()
            .Setup(e => e.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockGeneratedEmbedding);

        var expectedChunks = new List<DocumentChunk>
        {
            new(Guid.NewGuid(), "Result 1", "doc.pdf", mockVector, DateTimeOffset.UtcNow)
        };

        mocker.GetMock<IDocumentSession>()
            .Setup(s => s.QueryAsync<DocumentChunk>(
                It.Is<string>(sql => sql.Contains("<=>")),
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()))
            .ReturnsAsync(expectedChunks);

        // Act
        var result = await sut.SearchAsync(query, limit: 1);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].Content.Should().Be("Result 1");

        mocker.GetMock<IDocumentSession>().Verify(s =>
            s.QueryAsync<DocumentChunk>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task IngestChunksAsync_GeneratesEmbeddingsAndStoresInSession_CallsSaveChanges()
    {
        // Arrange
        var content = "This is a test chunk of text from the PDF.";
        var source = "SummerCamp_2026.pdf";
        var mockVector = new float[] { 0.5f, 0.5f, 0.5f };

        var mockGeneratedEmbedding = new GeneratedEmbeddings<Embedding<float>>(
            [new Embedding<float>(mockVector)]);

        mocker.GetMock<IEmbeddingGenerator<string, Embedding<float>>>()
            .Setup(e => e.GenerateAsync(
                It.Is<IEnumerable<string>>(s => s.First() == content),
                It.IsAny<EmbeddingGenerationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockGeneratedEmbedding);

        // Act
        await sut.IngestChunksAsync([content], source);

        // Assert
        mocker.GetMock<IDocumentSession>()
            .Verify(s => s.Store(It.Is<DocumentChunk>(chunk =>
                chunk.Content == content &&
                chunk.SourceDocument == source &&
                chunk.Embedding.SequenceEqual(mockVector))),
            Times.Once);

        mocker.GetMock<IDocumentSession>()
            .Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}