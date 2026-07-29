using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common.JsonConverters;

/// <summary>
/// 提供长整数的JSON字符串转换能力
/// </summary>
public class LongConverter : JsonConverter<long>
{

    /// <summary>
    /// 从JSON读取长整数
    /// </summary>
    /// <param name="reader">JSON读取器</param>
    /// <param name="typeToConvert">目标类型</param>
    /// <param name="options">序列化选项</param>
    /// <returns>长整数</returns>
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            if (long.TryParse(reader.GetString(), out long l))
            {
                return l;
            }
        }
        return reader.GetInt64();
    }


    /// <summary>
    /// 将长整数作为字符串写入JSON
    /// </summary>
    /// <param name="writer">JSON写入器</param>
    /// <param name="value">长整数</param>
    /// <param name="options">序列化选项</param>
    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
