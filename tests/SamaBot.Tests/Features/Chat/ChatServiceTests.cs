using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using SamaBot.Api.Features.Chat;
using System.Text;

namespace SamaBot.Tests.Features.Chat;

public class ChatServiceTests
{
    private readonly AutoMocker _mocker;
    private readonly ChatService _sut;
    private readonly BedrockSettings _defaultSettings;

    public ChatServiceTests()
    {
        _mocker = new AutoMocker();

        _defaultSettings = new BedrockSettings
        {
            ModelId = "anthropic.claude-3-haiku-20240307-v1:0",
            MaxTokens = 500,
            Temperature = 0.5f
        };

        _mocker.Use(Options.Create(_defaultSettings));

        _sut = _mocker.CreateInstance<ChatService>();
    }

    [Fact]
    public async Task GetResponseAsync_ReturnsParsedText_WhenBedrockRespondsCorrectly()
    {
        // Arrange
        var expectedResponseText = "¡Hola! Soy SamaBot y estoy listo para ayudar.";
        var systemPrompt = "Eres un asistente.";
        var userPrompt = "Hola";

        var jsonResponse = $$"""
        {
            "id": "msg_01XFDxyz",
            "type": "message",
            "role": "assistant",
            "content": [
                {
                    "type": "text",
                    "text": "{{expectedResponseText}}"
                }
            ]
        }
        """;

        var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonResponse));
        var invokeResponse = new InvokeModelResponse { Body = responseStream };

        _mocker.GetMock<IAmazonBedrockRuntime>()
            .Setup(c => c.InvokeModelAsync(It.IsAny<InvokeModelRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invokeResponse);

        // Act
        var result = await _sut.GetResponseAsync(systemPrompt, userPrompt, CancellationToken.None);

        // Assert
        result.Should().Be(expectedResponseText, "El servicio debería parsear correctamente el campo 'text' del JSON devuelto por Claude 3.");
    }

    [Fact]
    public async Task GetResponseAsync_SendsCorrectRequestToBedrock()
    {
        // Arrange
        var responseStream = new MemoryStream(Encoding.UTF8.GetBytes("""{"content":[{"text":"ok"}]}"""));
        _mocker.GetMock<IAmazonBedrockRuntime>()
            .Setup(c => c.InvokeModelAsync(It.IsAny<InvokeModelRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvokeModelResponse { Body = responseStream });

        // Act
        await _sut.GetResponseAsync("System", "User", CancellationToken.None);

        // Assert
        _mocker.GetMock<IAmazonBedrockRuntime>()
            .Verify(c => c.InvokeModelAsync(It.Is<InvokeModelRequest>(r =>
                r.ModelId == _defaultSettings.ModelId &&
                r.ContentType == "application/json" &&
                r.Accept == "application/json"
            ), It.IsAny<CancellationToken>()), Times.Once, "Debe llamar a Bedrock usando la configuración inyectada.");
    }
}