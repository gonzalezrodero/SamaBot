using JasperFx.Events;
using Marten;
using Microsoft.Extensions.AI;
using Npgsql;
using OllamaSharp;
using SamaBot.Api.Core.Entities;
using SamaBot.Api.Features.Knowledge;
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Marten")!;
var ollamaUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";

builder.Services.AddOpenApi();

// 1. Nos aseguramos de que la extensión vectorial existe en la DB
using (var conn = new NpgsqlConnection(connectionString))
{
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "CREATE EXTENSION IF NOT EXISTS vector;";
    cmd.ExecuteNonQuery();
}

// 2. DataSource normal. Ya NO necesitamos .UseVector() porque manejaremos los tipos en SQL
builder.Services.AddNpgsqlDataSource(connectionString);

builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
    new OllamaApiClient(new Uri(ollamaUrl), "nomic-embed-text"));
builder.Services.AddSingleton<IChatClient>(
    new OllamaApiClient(new Uri(ollamaUrl), "llama3"));

// 3. Configuración de Marten
builder.Services.AddMarten(opts =>
{
    opts.Events.StreamIdentity = StreamIdentity.AsString;
    opts.Storage.Add(new HnswIndexCustomizer());
})
.ApplyAllDatabaseChangesOnStartup()
.UseNpgsqlDataSource()
.UseLightweightSessions()
.IntegrateWithWolverine();

builder.Host.UseWolverine(opts =>
{
    opts.Policies.AutoApplyTransactions();
});

builder.Services.AddWolverineHttp();
builder.Services.AddKnowledgeFeature();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapWolverineEndpoints();

await app.RunAsync();