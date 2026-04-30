using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using SamaBot.Api.Common.Extensions;
using SamaBot.Api.Features.WhatsAppWebhook;
using System.Text.Json;
using Wolverine;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SamaBot.Api;

public class SqsLambdaHandler
{
    // Usamos Lazy para que la inicialización del Host (Marten, Wolverine, etc.) 
    // ocurra de forma segura y una sola vez durante el Cold Start de la Lambda.
    private static readonly Lazy<IServiceProvider> services = new(BuildWorkerProvider);

    private readonly IMessageBus bus;
    private readonly ILogger<SqsLambdaHandler> logger;
    private readonly JsonSerializerOptions jsonOptions;

    // Constructor sin parámetros requerido por AWS Lambda
    public SqsLambdaHandler()
    {
        bus = services.Value.GetRequiredService<IMessageBus>();
        logger = services.Value.GetRequiredService<ILogger<SqsLambdaHandler>>();
        jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    // Constructor para inyectar dependencias en Unit Tests (ideal para AutoMocker)
    public SqsLambdaHandler(IMessageBus bus, ILogger<SqsLambdaHandler> logger)
    {
        this.bus = bus;
        this.logger = logger;
        this.jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
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
        builder.Services.AddWolverine(builder.Configuration);

        var host = builder.Build();
        host.Start();
        return host.Services;
    }

    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext _)
    {
        logger.LogWarning(">>> [WORKER] Batch recibido con {Count} mensajes.", sqsEvent.Records.Count);

        foreach (var record in sqsEvent.Records)
        {
            logger.LogWarning(">>> [WORKER] RAW BODY DE SQS: {Body}", record.Body);

            var message = JsonSerializer.Deserialize<ProcessWhatsAppMessage>(record.Body, jsonOptions);

            if (message != null)
            {
                await bus.InvokeAsync(message);
            }
        }
    }
}