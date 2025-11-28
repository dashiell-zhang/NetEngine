using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SourceGenerator.Runtime;

/// <summary>
/// 为运行时提供统一的 JSON 序列化和反序列化工具
/// </summary>
public static class JsonUtil
{

    /// <summary>
    /// 默认的 JSON 序列化配置
    /// </summary>
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReferenceHandler = ReferenceHandler.Preserve,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };


    /// <summary>
    /// 将对象序列化为 JSON 字符串 在失败时退回为 ToString 结果
    /// </summary>
    /// <param name="value">待序列化的对象</param>
    /// <returns>JSON 字符串或回退字符串</returns>
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


    /// <summary>
    /// 将 JSON 字符串反序列化为通用对象 在失败时返回原始字符串
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>反序列化后的对象或原始字符串</returns>
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
