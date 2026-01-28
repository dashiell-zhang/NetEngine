using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common.JsonConverters;
public class DateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{


    private readonly string formatString;
    private readonly TimeSpan? timeZone;

    public DateTimeOffsetConverter()
    {
        formatString = "yyyy/MM/dd HH:mm:ss zzz";
        timeZone = null;
    }

    public DateTimeOffsetConverter(string inFormatString)
    {
        formatString = inFormatString;
        timeZone = null;
    }

    public DateTimeOffsetConverter(TimeSpan timeZone)
    {
        formatString = "yyyy/MM/dd HH:mm:ss zzz";
        this.timeZone = timeZone;
    }

    public DateTimeOffsetConverter(string inFormatString, TimeSpan? timeZone = null)
    {
        formatString = inFormatString;
        this.timeZone = timeZone;
    }

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


    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        var formatted = timeZone.HasValue ? value.ToUniversalTime().ToString(formatString) : value.ToString(formatString);
        writer.WriteStringValue(formatted);
    }
}
