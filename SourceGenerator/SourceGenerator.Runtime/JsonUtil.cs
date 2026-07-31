using System.Text;
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
    /// 用于生成稳定参数键的严格 JSON 序列化配置
    /// </summary>
    private static readonly JsonSerializerOptions KeyJsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
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
    /// 尝试将对象序列化为属性顺序稳定的规范化 JSON
    /// </summary>
    /// <param name="value">待序列化的对象</param>
    /// <param name="json">成功时返回规范化 JSON</param>
    /// <returns>如果对象可以完整稳定序列化则返回 true</returns>
    public static bool TryToCanonicalJson(object? value, out string json)
    {

        try
        {
            var runtimeType = value?.GetType();
            var element = JsonSerializer.SerializeToElement(value, runtimeType ?? typeof(object), KeyJsonOpts);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                if (runtimeType is null)
                {
                    element.WriteTo(writer);
                }
                else
                {
                    writer.WriteStartObject();
                    writer.WriteString("$type", GetStableTypeName(runtimeType));
                    writer.WritePropertyName("$value");
                    WriteCanonicalJson(writer, element);
                    writer.WriteEndObject();
                }

                writer.Flush();
            }

            json = Encoding.UTF8.GetString(stream.ToArray());
            return true;
        }
        catch
        {
            json = string.Empty;
            return false;
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


    /// <summary>
    /// 获取不包含程序集版本信息的稳定运行时类型名称
    /// </summary>
    /// <param name="type">待格式化的运行时类型</param>
    /// <returns>包含程序集简单名称的稳定类型名称</returns>
    private static string GetStableTypeName(Type type)
    {

        if (type.IsArray)
        {
            var elementType = type.GetElementType();
            return elementType is null
                ? type.Name
                : GetStableTypeName(elementType) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var arguments = string.Join(",", type.GetGenericArguments().Select(GetStableTypeName));
            return definition.Assembly.GetName().Name
                + "|"
                + (definition.FullName ?? definition.Name)
                + "["
                + arguments
                + "]";
        }

        return type.Assembly.GetName().Name + "|" + (type.FullName ?? type.Name);

    }


    /// <summary>
    /// 按照固定属性顺序写入 JSON 元素
    /// </summary>
    /// <param name="writer">JSON 写入器</param>
    /// <param name="element">待写入的 JSON 元素</param>
    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement element)
    {

        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();

            foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonicalJson(writer, property.Value);
            }

            writer.WriteEndObject();
            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();

            foreach (var item in element.EnumerateArray())
            {
                WriteCanonicalJson(writer, item);
            }

            writer.WriteEndArray();
            return;
        }

        element.WriteTo(writer);

    }
}
