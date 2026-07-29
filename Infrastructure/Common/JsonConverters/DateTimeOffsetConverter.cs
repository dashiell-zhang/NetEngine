using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common.JsonConverters;

/// <summary>
/// 提供DateTimeOffset的JSON格式转换能力
/// </summary>
public class DateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{


    private readonly string formatString;
    private readonly TimeSpan? timeZone;

    /// <summary>
    /// 使用默认时间格式初始化转换器
    /// </summary>
    public DateTimeOffsetConverter()
    {
        formatString = "yyyy/MM/dd HH:mm:ss zzz";
        timeZone = null;
    }

    /// <summary>
    /// 使用指定格式初始化转换器
    /// </summary>
    /// <param name="inFormatString">日期时间格式</param>
    public DateTimeOffsetConverter(string inFormatString)
    {
        formatString = inFormatString;
        timeZone = null;
    }

    /// <summary>
    /// 使用指定固定偏移初始化转换器
    /// </summary>
    /// <param name="timeZone">固定时间偏移</param>
    public DateTimeOffsetConverter(TimeSpan timeZone)
    {
        formatString = "yyyy/MM/dd HH:mm:ss zzz";
        this.timeZone = timeZone;
    }

    /// <summary>
    /// 使用指定格式和固定偏移初始化转换器
    /// </summary>
    /// <param name="inFormatString">日期时间格式</param>
    /// <param name="timeZone">固定时间偏移</param>
    public DateTimeOffsetConverter(string inFormatString, TimeSpan? timeZone = null)
    {
        formatString = inFormatString;
        this.timeZone = timeZone;
    }

    /// <summary>
    /// 从JSON读取带偏移的日期时间
    /// </summary>
    /// <param name="reader">JSON读取器</param>
    /// <param name="typeToConvert">目标类型</param>
    /// <param name="options">序列化选项</param>
    /// <returns>带偏移的日期时间</returns>
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            if (DateTimeOffset.TryParse(reader.GetString(), out DateTimeOffset date))
            {
                return timeZone.HasValue ? date.ToOffset(timeZone.Value) : date;
            }
        }

        var value = reader.GetDateTimeOffset();
        return timeZone.HasValue ? value.ToOffset(timeZone.Value) : value;
    }


    /// <summary>
    /// 将带偏移的日期时间写入JSON
    /// </summary>
    /// <param name="writer">JSON写入器</param>
    /// <param name="value">带偏移的日期时间</param>
    /// <param name="options">序列化选项</param>
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        var formatted = timeZone.HasValue ? value.ToOffset(timeZone.Value).ToString(formatString) : value.ToString(formatString);
        writer.WriteStringValue(formatted);
    }
}
