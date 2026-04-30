
using Amazon.BedrockRuntime.Model;
using JasperFx;
using JasperFx.CodeGeneration;
using SamaBot.Api;
using SamaBot.Api.Common.Extensions;
using SamaBot.Api.Features.WhatsAppWebhook; // Added to access ProcessWhatsAppMessage
using Wolverine;
using Wolverine.AmazonSqs;
using Wolverine.ErrorHandling;
using Wolverine.Http;

var builder = WebApplication.CreateBuilder(args);

// Clear default providers to avoid duplicates or weird formatting in AWS
builder.Logging.ClearProviders();

// Configure JSON logging (ideal for CloudWatch)
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = false;
    options.TimestampFormat = "HH:mm:ss ";
    options.JsonWriterOptions = new System.Text.Json.JsonWriterOptions
    {
        Indented = false // Keep it false so each log is a single line in CloudWatch
    };
});

// Set the global minimum level
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Filter out .NET "noise" to avoid cluttering CloudWatch and saving costs
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("System", LogLevel.Warning);

// Load AWS Secrets dynamically before initializing Marten
builder.AddAwsSecureConfiguration();

// Configuration Variables
var connectionString = builder.Configuration.GetConnectionString("Marten")!;

// 1. Core Services
builder.Services.AddOpenApi();
builder.Services.AddWolverineHttp();

// 2. Domain & Infrastructure Extensions
builder.Services.AddDatabase(connectionString);
builder.Services.AddAi(builder.Configuration);
builder.Services.AddFeatures(builder.Configuration);

// 3. Host Configuration (Wolverine)
builder.Host.UseWolverine(opts =>
{
    opts.Policies.AutoApplyTransactions();
    opts.Policies.OnException<ThrottlingException>()
        .RetryWithCooldown(
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(1)
    );

    var sqs = opts.UseAmazonSqsTransport();

    if (builder.Environment.IsEnvironment("Testing"))
    {
        opts.CodeGeneration.TypeLoadMode = TypeLoadMode.Dynamic;
        sqs.AutoProvision();
    }
    else
    {
        sqs.SystemQueuesAreEnabled(false);
    }

    opts.PublishMessage<ProcessWhatsAppMessage>().ToSqsQueue("chatbot-messages-queue");
    opts.ListenToSqsQueue("chatbot-messages-queue");
});

builder.Services.AddHealthChecks();

// Lambda Hosting (Automatically ignored when running locally)
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

var app = builder.Build();

// 4. Initialization Phase
if (!args.Contains("codegen"))
{
    app.EnsureVectorExtensionExists(connectionString);
}

// 5. HTTP Pipeline Configuration
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapWolverineEndpoints();
app.MapHealthChecks("/health");

return await app.RunJasperFxCommands(args);