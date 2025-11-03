using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SourceGenerator.Runtime;

public static class JsonUtil
{
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
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



    public static object ToObject(string? json)
    {
        if (string.IsNullOrEmpty(json)) return json ?? string.Empty;

        try
        {
            var obj = JsonSerializer.Deserialize<object>(json, JsonOpts);
            if (obj != null) return obj;
        }
        catch
        {

        }

        return json;
    }
}
