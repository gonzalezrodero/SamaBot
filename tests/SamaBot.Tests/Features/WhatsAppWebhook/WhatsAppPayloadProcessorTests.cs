using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq.AutoMock;
using SamaBot.Api.Features.WhatsAppWebhook;
using System.Security.Cryptography;
using System.Text;

namespace SamaBot.Tests.Features.WhatsAppWebhook;

public class WhatsAppPayloadProcessorTests
{
    private readonly AutoMocker _mocker;
    private readonly WhatsAppPayloadProcessor _sut;
    private const string TestSecret = "my_super_secret_test_key";
    
    public WhatsAppPayloadProcessorTests()
    {
        _mocker = new AutoMocker();
        
        // Setup the configuration mock before injecting it
        var configMock = _mocker.GetMock<IConfiguration>();
        configMock.Setup(c => c["WhatsApp:App_Secret"]).Returns(TestSecret);
        
        _sut = _mocker.CreateInstance<WhatsAppPayloadProcessor>();
    }

    [Fact]
    public async Task IsSignatureValidAsync_WithCorrectSignature_ReturnsTrue()
    {
        // Arrange
        var payload = """{"test":"payload"}""";
        var expectedHash = ComputeHmacSha256(payload, TestSecret);
        
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Hub-Signature-256"] = $"sha256={expectedHash}";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        // Act
        var result = await _sut.IsSignatureValidAsync(context.Request);

        // Assert
        result.Should().BeTrue();
    }
    
    [Fact]
    public async Task IsSignatureValidAsync_WithInvalidSignature_ReturnsFalse()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Hub-Signature-256"] = "sha256=invalid_hash";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("""{"test":"payload"}"""));

        // Act
        var result = await _sut.IsSignatureValidAsync(context.Request);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExtractMessageAsync_WithValidMetaPayload_ReturnsParsedMessage()
    {
        // Arrange
        var payload = """
        {
          "object": "whatsapp_business_account",
          "entry": [
            {
              "changes": [
                {
                  "value": {
                    "metadata": { "phone_number_id": "12345" },
                    "messages": [
                      {
                        "from": "34666555444",
                        "id": "wamid.HBgL",
                        "timestamp": "1603059201",
                        "text": { "body": "Hola SamàBot!" },
                        "type": "text"
                      }
                    ]
                  }
                }
              ]
            }
          ]
        }
        """;
        
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        // Act
        var result = await _sut.ExtractMessageAsync(context.Request);

        // Assert
        result.Should().NotBeNull();
        result!.MessageId.Should().Be("wamid.HBgL");
        result.BotPhoneNumberId.Should().Be("12345");
        result.PhoneNumber.Should().Be("34666555444");
        result.Text.Should().Be("Hola SamàBot!");
        result.Timestamp.ToUnixTimeSeconds().Should().Be(1603059201);
    }

    private static string ComputeHmacSha256(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hashBytes);
    }
}
