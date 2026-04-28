
using Amazon.BedrockRuntime.Model;
using JasperFx;
using SamaBot.Api;
using SamaBot.Api.Common.Extensions;
using SamaBot.Api.Features.WhatsAppWebhook; // Added to access ProcessWhatsAppMessage
using Wolverine;
using Wolverine.AmazonSqs;
using Wolverine.ErrorHandling;
using Wolverine.Http;

var builder = WebApplication.CreateBuilder(args);

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

    opts.UseAmazonSqsTransport().AutoProvision();

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