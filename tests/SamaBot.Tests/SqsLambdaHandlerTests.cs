using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using AwesomeAssertions;
using Moq;
using SamaBot.Api;
using Wolverine;

namespace SamaBot.Tests;

public class SqsLambdaHandlerTests
{
    private readonly Mock<IMessageBus> busMock = new();
    private readonly Mock<ILambdaContext> contextMock = new();
    private readonly SqsLambdaHandler handler;

    public SqsLambdaHandlerTests()
    {
        handler = new SqsLambdaHandler(busMock.Object);
    }

    [Fact]
    public async Task FunctionHandler_ShouldInvokeBus_ForEachSqsRecord()
    {
        // Arrange
        var body1 = "{ \"message\": \"test 1\" }";
        var body2 = "{ \"message\": \"test 2\" }";

        var sqsEvent = new SQSEvent
        {
            Records =
            [
                new() { Body = body1 },
                new() { Body = body2 }
            ]
        };

        // Act
        await handler.FunctionHandler(sqsEvent, contextMock.Object);

        // Assert
        busMock.Verify(x => x.InvokeAsync(body1, default, null), Times.Once);
        busMock.Verify(x => x.InvokeAsync(body2, default, null), Times.Once);
        busMock.Invocations.Count.Should().Be(2);
    }

    [Fact]
    public async Task FunctionHandler_WithEmptyRecords_ShouldNotInvokeBus()
    {
        // Arrange
        var sqsEvent = new SQSEvent { Records = [] };

        // Act
        await handler.FunctionHandler(sqsEvent, contextMock.Object);

        // Assert
        busMock.Verify(x => x.InvokeAsync(It.IsAny<object>(), default, null), Times.Never);
    }
}