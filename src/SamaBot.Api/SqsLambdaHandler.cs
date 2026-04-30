using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using SamaBot.Api.Common.Extensions;
using SamaBot.Api.Features.WhatsAppWebhook;
using System.Text.Json;
using Wolverine;

namespace SamaBot.Api;

public class SqsLambdaHandler
{
    private static readonly IServiceProvider services = BuildWorkerProvider();
    private readonly IMessageBus bus;
    private readonly ILogger<SqsLambdaHandler> logger;

    public SqsLambdaHandler()
    {
        bus = services.GetRequiredService<IMessageBus>();
        logger = services.GetRequiredService<ILogger<SqsLambdaHandler>>();
    }

    public SqsLambdaHandler(IMessageBus bus, ILogger<SqsLambdaHandler> logger)
    {
        this.bus = bus;
        this.logger = logger;
    }

    private static IServiceProvider BuildWorkerProvider()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Logging.AddLogging();
        builder.AddAwsSecureConfiguration();

        var conn = builder.Configuration.GetConnectionString("Marten")!;

        builder.Services.AddDatabase(conn);
        builder.Services.AddAi(builder.Configuration);
        builder.Services.AddFeatures(builder.Configuration);
        builder.Services.AddWolverine();

        var host = builder.Build();
        host.Start();
        return host.Services;
    }

    public JsonSerializerOptions GetOptions()
    {
        return new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext _, JsonSerializerOptions options)
    {
        logger.LogWarning(">>> [WORKER] Batch recibido con {Count} mensajes.", sqsEvent.Records.Count);

        foreach (var record in sqsEvent.Records)
        {
            var message = JsonSerializer.Deserialize<ProcessWhatsAppMessage>(record.Body, options);

            if (message != null)
            {
                await bus.InvokeAsync(message);
            }
        }
    }
}