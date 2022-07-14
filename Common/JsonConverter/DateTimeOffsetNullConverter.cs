using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common.JsonConverter
{
    public class DateTimeOffsetNullConverter : JsonConverter<DateTimeOffset?>
    {


        private readonly string formatString;
        public DateTimeOffsetNullConverter()
        {
            formatString = "yyyy/MM/dd HH:mm:ss zzz";
        }

        public DateTimeOffsetNullConverter(string inFormatString)
        {
            formatString = inFormatString;
        }


        public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return string.IsNullOrEmpty(reader.GetString()) ? default(DateTimeOffset?) : DateTimeOffset.Parse(reader.GetString()!);
        }


        public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value?.ToString(formatString));
        }
    }

}
