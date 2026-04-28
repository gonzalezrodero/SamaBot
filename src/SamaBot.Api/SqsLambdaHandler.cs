using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Wolverine;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SamaBot.Api;

// The SqsLambdaHandler is the entry point for the Worker
public class SqsLambdaHandler(IMessageBus bus)
{
    // This method is triggered by AWS SQS events
    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext _)
    {
        foreach (var record in sqsEvent.Records)
        {
            // We pass the raw message body to Wolverine's internal bus
            // This triggers the specific handlers (Marten, Bedrock, etc.) in this process
            await bus.InvokeAsync(record.Body);
        }
    }
}
