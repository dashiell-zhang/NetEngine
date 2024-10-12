using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common.JsonConverter
{
    public class DateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {


        private readonly string formatString;
        public DateTimeOffsetConverter()
        {
            formatString = "yyyy/MM/dd HH:mm:ss zzz";
        }

        public DateTimeOffsetConverter(string inFormatString)
        {
            formatString = inFormatString;
        }

        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                if (DateTimeOffset.TryParse(reader.GetString(), out DateTimeOffset date))
                {
                    return date;
                }
            }
            return reader.GetDateTimeOffset();
        }


        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(formatString));
        }
    }

}
