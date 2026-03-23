using JasperFx.Events;
using Marten;
using Microsoft.Extensions.AI;
using OllamaSharp;
using SamaBot.Api;
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

// Configure Marten for Event Sourcing
var connectionString = builder.Configuration.GetConnectionString("Postgres") 
    ?? "Host=localhost;Database=samabot;Username=postgres;Password=password";

builder.Services.AddMarten(opts =>
{
    opts.Connection(connectionString);
    opts.Events.StreamIdentity = StreamIdentity.AsString;
}).IntegrateWithWolverine();

// Register all Domain Features
builder.Services.AddSamaBotFeatures(builder.Configuration);

// Bootstrapping the default LLM ChatClient via Microsoft.Extensions.AI
// Using Ollama as the base local provider for cost efficiency during development
builder.Services.AddChatClient(new OllamaApiClient(new Uri("http://localhost:11434"), "llama3"));

// CRITICAL: Register Wolverine HTTP capabilities for Minimal APIs
builder.Services.AddWolverineHttp();

// Configure Wolverine with Mediator and Outbox pattern
builder.Host.UseWolverine(opts =>
{
    opts.Policies.AutoApplyTransactions();
});

var app = builder.Build();

// Map Wolverine Minimal HTTP Endpoints
app.MapWolverineEndpoints();

app.Run();

// Required so Testcontainers & Alba can spawn the app instance
public partial class Program { }
