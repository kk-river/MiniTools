using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppRunner;

internal record Parameter
(
    string Key,
    string? Operator,
    string? Value,
    Preprocessor[]? Preprocessors
)
{
    public string ToParamString()
    {
        DefaultInterpolatedStringHandler handler = new(0, 0);
        handler.AppendLiteral(Key);

        if (Value is not null)
        {
            handler.AppendLiteral(Operator ?? " ");
            handler.AppendLiteral(ProcessValue(Value));
        }

        return handler.ToString();
    }

    private string ProcessValue(string original)
    {
        if (Preprocessors is null) { return original; }

        string processed = original;
        foreach (Preprocessor preprocessor in Preprocessors)
        {
            processed = preprocessor.Process(processed);
        }

        return processed;
    }
}

[JsonSourceGenerationOptions
(
    AllowTrailingCommas = true,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip
)]
[JsonSerializable(typeof(Parameter))]
internal partial class ParameterContext : JsonSerializerContext;
