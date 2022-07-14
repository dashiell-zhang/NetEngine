using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdminApp.Libraries.Json
{
    public class DateTimeNullConverter : JsonConverter<DateTime?>
    {


        public DateTimeNullConverter()
        {

        }



        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return string.IsNullOrEmpty(reader.GetString()) ? default(DateTime?) : DateTime.Parse(reader.GetString()!);
        }


        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value?.ToString());
        }
    }

}
