using Pgvector;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SamaBot.Api.Features.Knowledge;

public class VectorJsonConverter : JsonConverter<Vector>
{
    public override Vector Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var array = JsonSerializer.Deserialize<float[]>(ref reader, options);
        return new Vector(array!);
    }
    public override void Write(Utf8JsonWriter writer, Vector value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.ToArray(), options);
    }
}