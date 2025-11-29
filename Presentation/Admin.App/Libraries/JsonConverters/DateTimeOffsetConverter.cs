using System.Text.Json;
using System.Text.Json.Serialization;

namespace Admin.App.Libraries.JsonConverters;
public class DateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{

    private readonly string formatString;
    private readonly TimeSpan timeZone;

    public DateTimeOffsetConverter()
    {
        formatString = "yyyy-MM-ddTHH:mm:sszzz";
        timeZone = DateTimeOffset.Now.Offset;
    }

    public DateTimeOffsetConverter(string inFormatString)
    {
        formatString = inFormatString;
        timeZone = DateTimeOffset.Now.Offset;
    }

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            if (DateTimeOffset.TryParse(reader.GetString(), out DateTimeOffset date))
            {
                return date.ToOffset(timeZone);
            }
        }
        return reader.GetDateTimeOffset().ToOffset(timeZone);
    }


    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToUniversalTime().ToString(formatString));
    }
}

