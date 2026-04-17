using JasperFx;
using SamaBot.Api;
using Wolverine;
using Wolverine.Http;

var builder = WebApplication.CreateBuilder(args);

// Configuration Variables
var connectionString = builder.Configuration.GetConnectionString("Marten")!;

// Remove hardcoded Ollama URL to fix SonarQube issue
var ollamaUrl = builder.Configuration["Ollama:BaseUrl"]
                ?? throw new InvalidOperationException("Ollama:BaseUrl is missing.");

// 1. Core Services
builder.Services.AddOpenApi();
builder.Services.AddWolverineHttp();

// 2. Domain & Infrastructure Extensions
builder.Services.AddDatabase(connectionString);
builder.Services.AddAi(ollamaUrl);
builder.Services.AddFeatures(builder.Configuration);

// 3. Host Configuration (Wolverine)
builder.Host.UseWolverine(opts =>
{
    opts.Policies.AutoApplyTransactions();
});

builder.Services.AddHealthChecks();
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

app.UseHttpsRedirection();
app.MapWolverineEndpoints();

app.MapHealthChecks("/health");
return await app.RunJasperFxCommands(args);