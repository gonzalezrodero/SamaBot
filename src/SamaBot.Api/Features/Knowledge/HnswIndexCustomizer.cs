using SamaBot.Api.Core.Entities;
using System.Diagnostics.CodeAnalysis;
using Weasel.Core;
using Weasel.Core.Migrations;
using Weasel.Postgresql;

namespace SamaBot.Api.Features.Knowledge;

public class HnswIndexCustomizer : IFeatureSchema
{
    public IEnumerable<Type> DependentTypes() => [typeof(DocumentChunk)];
    public ISchemaObject[] Objects => [];
    public string Identifier => "hnsw-vector-index";

    public Migrator Migrator => new PostgresqlMigrator();
    public Type StorageType => typeof(HnswIndexCustomizer);

    public void WritePermissions(Migrator rules, TextWriter writer) { }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Required by IFeatureSchema interface")]
    public void WriteTemplate(Migrator rules, TextWriter writer)
    {
        writer.WriteLine(@"
            CREATE INDEX IF NOT EXISTS mt_doc_documentchunk_idx_embedding 
            ON public.mt_doc_documentchunk USING hnsw ((CAST(data ->> 'Embedding' AS vector(768))) vector_cosine_ops);
        ");
    }
}