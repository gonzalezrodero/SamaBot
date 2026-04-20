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
    public async Task Ingest_EmptyFile_ReturnsBadRequest()
    {
        // Arrange
        var fileMock = mocker.GetMock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(0); // Simulate empty file

        var service = mocker.GetMock<IPdfIngestionService>().Object;

        // Act
        var result = await sut.Ingest(fileMock.Object, service, CancellationToken.None);

        // Assert
        result.Should().BeAssignableTo<IStatusCodeHttpResult>()
              .Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Ingest_InvalidContentType_ReturnsBadRequest()
    {
        // Arrange
        var fileMock = mocker.GetMock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(100);
        fileMock.Setup(f => f.ContentType).Returns("image/png"); // Not a PDF

        var service = mocker.GetMock<IPdfIngestionService>().Object;

        // Act
        var result = await sut.Ingest(fileMock.Object, service, CancellationToken.None);

        // Assert
        result.Should().BeAssignableTo<IStatusCodeHttpResult>()
              .Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Ingest_ServiceThrowsGenericException_ReturnsProblem()
    {
        // Arrange
        var fileMock = mocker.GetMock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(100);
        fileMock.Setup(f => f.ContentType).Returns("application/pdf");
        fileMock.Setup(f => f.FileName).Returns("error.pdf");
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

        var serviceMock = mocker.GetMock<IPdfIngestionService>();

        serviceMock
            .Setup(x => x.IngestPdfStreamAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Random failure in Bedrock or DB"));

        // Act
        var result = await sut.Ingest(fileMock.Object, serviceMock.Object, CancellationToken.None);

        // Assert
        result.Should().BeAssignableTo<IStatusCodeHttpResult>()
              .Which.StatusCode.Should().Be(500);
    }
}