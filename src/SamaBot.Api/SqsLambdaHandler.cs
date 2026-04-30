using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using SamaBot.Api;
using SamaBot.Api.Common.Extensions;
using Wolverine;

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

    private static IServiceProvider BuildWorkerProvider()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Logging.AddSamaBotLogging();
        builder.AddAwsSecureConfiguration();

        var conn = builder.Configuration.GetConnectionString("Marten")!;

        builder.Services.AddDatabase(conn);
        builder.Services.AddAi(builder.Configuration);
        builder.Services.AddFeatures(builder.Configuration);
        builder.Services.AddWolverine(builder.Environment);

        var host = builder.Build();
        host.Start();
        return host.Services;
    }

    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext _)
    {
        logger.LogWarning(">>> [WORKER] Batch recibido con {Count} mensajes.", sqsEvent.Records.Count);
        foreach (var record in sqsEvent.Records)
        {
            await bus.InvokeAsync(record.Body);
        }
    }
}