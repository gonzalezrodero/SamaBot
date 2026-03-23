using AwesomeAssertions;
using Microsoft.Extensions.AI;
using Moq;
using Moq.AutoMock;
using SamaBot.Api.Features.LanguageDetection;

namespace SamaBot.Tests.Features.LanguageDetection;

public class LanguageDetectorTests
{
    private readonly AutoMocker _mocker;
    private readonly LanguageDetector _sut;

    public LanguageDetectorTests()
    {
        _mocker = new AutoMocker();
        _sut = _mocker.CreateInstance<LanguageDetector>();
    }

    [Theory]
    [InlineData("es", "es")]
    [InlineData("en", "en")]
    [InlineData("ca", "ca")]
    [InlineData(" ES ", "es")] // Tests trimming
    [InlineData("INVALID", "ca")] // Tests fallback constraint
    [InlineData("", "ca")] // Tests empty fallback
    public async Task DetectLanguageAsync_ReturnsEnforcedISOFormat(string mockLlmResponse, string expectedResult)
    {
        // Arrange
        var mockResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, mockLlmResponse));
        
        _mocker.GetMock<IChatClient>()
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _sut.DetectLanguageAsync("Some text to analyze");

        // Assert
        result.Should().Be(expectedResult);
    }
}
