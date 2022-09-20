using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common.JsonConverter
{
    public class NullableConverter<T> : JsonConverter<T?> where T : struct
    {

        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                if (string.IsNullOrEmpty(reader.GetString()) || string.IsNullOrWhiteSpace(reader.GetString()))
                {
                    return null;
                }
            }
            return JsonSerializer.Deserialize<T>(ref reader, options);
        }


        public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value!.Value, options);
        }

    }
}
