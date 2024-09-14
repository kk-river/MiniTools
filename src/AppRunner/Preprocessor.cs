using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppRunner;

internal record Preprocessor
(
    string Type,
    string? From,
    string? To
)
{
    public string Process(string value)
    {
        return Type switch
        {
            "replace" => ProcessReplace(value),
            "convertToAbsolutePath" => ProcessConvertAbsolute(value),
            _ => throw new ArgumentException($"Unknown preprocessor type: {Type}."),
        };
    }

    private string ProcessReplace(string value)
    {
        if (From is null)
        {
            throw new ArgumentNullException(nameof(From), $"Replace preprocessor requires a value for {nameof(From)}.");
        }

        return value.Replace(From, To);
    }

    private static string ProcessConvertAbsolute(string value) => Path.GetFullPath(value);
}

[JsonSourceGenerationOptions
(
    AllowTrailingCommas = true,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip
)]
[JsonSerializable(typeof(Preprocessor))]
internal partial class PreprocessorContext : JsonSerializerContext;
