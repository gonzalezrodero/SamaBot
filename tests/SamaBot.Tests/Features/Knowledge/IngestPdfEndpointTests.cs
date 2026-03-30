using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Moq.AutoMock;
using SamaBot.Api.Features.Knowledge;

namespace SamaBot.Tests.Features.Knowledge;

public class IngestPdfEndpointTests
{
    private readonly AutoMocker mocker;
    private readonly IngestPdfEndpoint sut;

    public IngestPdfEndpointTests()
    {
        mocker = new AutoMocker();
        sut = mocker.CreateInstance<IngestPdfEndpoint>();
    }

    [Fact]
    public async Task Ingest_EmptyFilePath_ReturnsBadRequest()
    {
        // Arrange
        var request = new IngestPdfRequest("");
        var service = mocker.GetMock<IPdfIngestionService>().Object;

        // Act
        var result = await sut.Ingest(request, service, CancellationToken.None);

        // Assert
        result.Should().BeAssignableTo<IStatusCodeHttpResult>()
              .Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Ingest_ServiceThrowsFileNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new IngestPdfRequest("missing.pdf");
        var serviceMock = mocker.GetMock<IPdfIngestionService>();

        serviceMock
            .Setup(x => x.IngestPdfAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("File not found"));

        // Act
        var result = await sut.Ingest(request, serviceMock.Object, CancellationToken.None);

        // Assert
        result.Should().BeAssignableTo<IStatusCodeHttpResult>()
              .Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Ingest_ServiceThrowsGenericException_ReturnsProblem()
    {
        // Arrange
        var request = new IngestPdfRequest("error.pdf");
        var serviceMock = mocker.GetMock<IPdfIngestionService>();

        serviceMock
            .Setup(x => x.IngestPdfAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Random failure"));

        // Act
        var result = await sut.Ingest(request, serviceMock.Object, CancellationToken.None);

        // Assert
        result.Should().BeAssignableTo<IStatusCodeHttpResult>()
              .Which.StatusCode.Should().Be(500);
    }
}