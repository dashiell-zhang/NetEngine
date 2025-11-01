using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SourceGenerator.Runtime;

public static class JsonUtil
{
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReferenceHandler = ReferenceHandler.Preserve,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string ToJson(object? value)
    {
        try
        {
            return JsonSerializer.Serialize(value, JsonOpts);
        }
        catch
        {
            return value?.ToString() ?? "<null>";
        }
    }
}
