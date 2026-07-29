using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common.JsonConverters;

/// <summary>
/// 提供DateTime的JSON格式转换能力
/// </summary>
public class DateTimeConverter : JsonConverter<DateTime>
{


    private readonly string formatString;

    /// <summary>
    /// 使用默认时间格式初始化转换器
    /// </summary>
    public DateTimeConverter()
    {
        formatString = "yyyy/MM/dd HH:mm:ss";
    }

    /// <summary>
    /// 使用指定格式初始化转换器
    /// </summary>
    /// <param name="inFormatString">日期时间格式</param>
    public DateTimeConverter(string inFormatString)
    {
        formatString = inFormatString;
    }

    /// <summary>
    /// 从JSON读取日期时间
    /// </summary>
    /// <param name="reader">JSON读取器</param>
    /// <param name="typeToConvert">目标类型</param>
    /// <param name="options">序列化选项</param>
    /// <returns>日期时间</returns>
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            if (DateTime.TryParse(reader.GetString(), out DateTime date))
            {
                return date;
            }
        }
        return reader.GetDateTime();
    }


    /// <summary>
    /// 将日期时间写入JSON
    /// </summary>
    /// <param name="writer">JSON写入器</param>
    /// <param name="value">日期时间</param>
    /// <param name="options">序列化选项</param>
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(formatString));
    }
}
