using System.Diagnostics.CodeAnalysis;
using JasperFx.Events;
using Marten;
using Microsoft.Extensions.AI;
using OllamaSharp;
using SamaBot.Api;
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("Marten")!;

// Registering Marten for Event Sourcing and persistence 
builder.Services.AddMarten(opts =>
{
    opts.Connection(connectionString);
    opts.Events.StreamIdentity = StreamIdentity.AsString;
}).IntegrateWithWolverine();

// Register all Domain Features
builder.Services.AddSamaBotFeatures(builder.Configuration);

// Bootstrapping the default LLM ChatClient via Microsoft.Extensions.AI
// Using Ollama as the base local provider for cost efficiency during development
var ollamaUrl = builder.Configuration["Ollama:BaseUrl"] ?? "";
builder.Services.AddChatClient(new OllamaApiClient(new Uri(ollamaUrl), "llama3"));

// CRITICAL: Register Wolverine HTTP capabilities for Minimal APIs
builder.Services.AddWolverineHttp();

// Configure Wolverine with Mediator and Outbox pattern
builder.Host.UseWolverine(opts =>
{
    opts.Policies.AutoApplyTransactions();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Map Wolverine Minimal HTTP Endpoints
app.MapWolverineEndpoints();

await app.RunAsync();

// Required so Testcontainers & Alba can spawn the app instance
public partial class Program
{
    [ExcludeFromCodeCoverage]
    protected Program() { }
}
