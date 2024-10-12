using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common.JsonConverter
{

    public class NullableStructConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            if (typeToConvert.GetProperty("HasValue") != null)
            {
                return true;
            }
            return false;
        }

        public override System.Text.Json.Serialization.JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var converterType = typeof(NullableConverter<>).MakeGenericType(typeToConvert.GenericTypeArguments[0]);
            return (System.Text.Json.Serialization.JsonConverter)Activator.CreateInstance(converterType)!;
        }


        private class NullableConverter<T> : JsonConverter<T?> where T : struct
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
                var tempOptions = new JsonSerializerOptions(options);

                var thisFactory = new NullableStructConverterFactory().ToString();
                tempOptions.Converters.Remove(options.Converters.FirstOrDefault(t => t.ToString() == thisFactory)!);

                JsonSerializer.Serialize(writer, value, tempOptions);
            }

        }
    }
}
