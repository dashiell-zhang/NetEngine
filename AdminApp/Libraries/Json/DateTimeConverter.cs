using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdminApp.Libraries.Json
{
    public class DateTimeConverter : JsonConverter<DateTime>
    {


        public DateTimeConverter()
        {

        }


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


        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

}
