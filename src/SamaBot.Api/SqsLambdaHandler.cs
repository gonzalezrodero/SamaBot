using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using SamaBot.Api.Common.Extensions;
using SamaBot.Api.Features.WhatsAppWebhook;
using System.Runtime.Loader; // Necesario para AssemblyLoadContext
using System.Text.Json;
using Wolverine;

namespace SamaBot.Api;

public class SqsLambdaHandler
{
    // 1. EL INTERCEPTOR (Constructor estático)
    // Se ejecuta absoluta y estrictamente antes de cualquier otra cosa en la clase.
    static SqsLambdaHandler()
    {
        AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
        {
            if (assemblyName.Name == "SnapshotRestore.Registry")
            {
                // Ignoramos la versión que pidan y devolvemos la que AWS tiene cargada
                return AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "SnapshotRestore.Registry");
            }
            return null;
        };
    }

    // 2. Usamos Lazy para que BuildWorkerProvider se ejecute DESPUÉS del interceptor, 
    // justo en el momento en el que AWS instancia la clase.
    private static readonly Lazy<IServiceProvider> services = new(BuildWorkerProvider);

    private readonly IMessageBus bus;
    private readonly ILogger<SqsLambdaHandler> logger;
    private readonly JsonSerializerOptions jsonOptions;

    // Constructor para AWS
    public SqsLambdaHandler()
    {
        bus = services.Value.GetRequiredService<IMessageBus>();
        logger = services.Value.GetRequiredService<ILogger<SqsLambdaHandler>>();
        jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    // Constructor para tus Unit Tests
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
        builder.Services.AddWolverine();

        var host = builder.Build();
        host.Start();
        return host.Services;
    }

    // 3. Firma corregida: Solo 2 parámetros permitidos por AWS SQS
    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext _)
    {
        logger.LogWarning(">>> [WORKER] Batch recibido con {Count} mensajes.", sqsEvent.Records.Count);

        foreach (var record in sqsEvent.Records)
        {
            var message = JsonSerializer.Deserialize<ProcessWhatsAppMessage>(record.Body, jsonOptions);

            if (message != null)
            {
                await bus.InvokeAsync(message);
            }
        }
    }
}