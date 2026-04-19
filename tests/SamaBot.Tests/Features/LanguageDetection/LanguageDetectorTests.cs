using AwesomeAssertions;
using Moq;
using Moq.AutoMock;
using SamaBot.Api.Features.Chat;
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
    [InlineData(null, "ca")] // Tests null fallback (buena práctica añadir este también)
    [InlineData("", "ca")] // Tests empty fallback
    public async Task DetectLanguageAsync_ReturnsEnforcedISOFormat(string? mockLlmResponse, string expectedResult)
    {
        // Arrange
        _mocker.GetMock<IChatService>()
            .Setup(c => c.GetResponseAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockLlmResponse!);

        // Act
        var result = await _sut.DetectLanguageAsync("Some text to analyze");

        // Assert
        result.Should().Be(expectedResult);
    }
}