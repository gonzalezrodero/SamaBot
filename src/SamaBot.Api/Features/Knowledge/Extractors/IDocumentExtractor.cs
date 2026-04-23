namespace SamaBot.Api.Features.Knowledge.Extractors;

public interface IDocumentExtractor
{
    string[] SupportedExtensions { get; }
    Task<string> ExtractTextAsync(Stream stream);
}
