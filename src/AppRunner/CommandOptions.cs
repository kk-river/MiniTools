using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppRunner;

internal record CommandOptions
(
    string? WorkingDirectory,
    Parameter[]? Parameters
)
{
    public string ParamString => Parameters is null ? "" : string.Join(" ", Parameters.Select(p => p.ToParamString()));
}


[JsonSourceGenerationOptions
(
    AllowTrailingCommas = true,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip
)]
[JsonSerializable(typeof(CommandOptions))]
internal partial class OptionsContext : JsonSerializerContext;
