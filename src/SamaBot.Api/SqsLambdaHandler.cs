using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Wolverine;
using Microsoft.Extensions.Logging;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SamaBot.Api;

public class SqsLambdaHandler(IMessageBus bus, ILogger<SqsLambdaHandler> logger)
{
    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        logger.LogWarning(">>> [WORKER DEBUG] 🚀 Worker despertado. Mensajes en el batch: {Count}", sqsEvent.Records.Count);

        foreach (var record in sqsEvent.Records)
        {
            logger.LogWarning(">>> [WORKER DEBUG] 📦 Procesando mensaje ID: {MessageId}", record.MessageId);
            logger.LogWarning(">>> [WORKER DEBUG] 📄 Body crudo recibido de SQS: {Body}", record.Body);

            try
            {
                // Le pasamos el string a Wolverine. 
                // Nota: Dependiendo de la versión de Wolverine, InvokeAsync(string) puede
                // necesitar que el string sea un Envelope JSON válido.
                await bus.InvokeAsync(record.Body);

                logger.LogWarning(">>> [WORKER DEBUG] ✅ Wolverine terminó de procesar el mensaje sin lanzar excepciones.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ">>> [WORKER ERROR] ❌ Wolverine explotó al intentar invocar el handler.");

                // Es MUY importante relanzar la excepción. 
                // Si la atrapamos en silencio, AWS SQS creerá que el mensaje se procesó bien y lo borrará.
                // Si la relanzamos, AWS SQS sabrá que falló y lo mandará a la Dead Letter Queue (DLQ).
                throw;
            }
        }
    }
}