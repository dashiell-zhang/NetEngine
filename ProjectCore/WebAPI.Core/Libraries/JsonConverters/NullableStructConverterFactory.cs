using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebAPI.Core.Libraries.JsonConverters
{

    public class NullableStructConverterFactory : JsonConverterFactory
    {


        private static JsonSerializerOptions serializeOpts;


        public override bool CanConvert(Type typeToConvert)
        {
            if (typeToConvert.GetProperty("HasValue") != null)
            {
                return true;
            }
            return false;
        }


        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            if (serializeOpts == null)
            {
                serializeOpts = new(options);

                NullableStructConverterFactory factoryConverter = new();

                var existingConverter = serializeOpts.Converters.FirstOrDefault(t => t.GetType() == factoryConverter.GetType());

                if (existingConverter != null)
                {
                    serializeOpts.Converters.Remove(existingConverter);
                }
            }


            var converterType = typeof(NullableStructConverter<>).MakeGenericType(typeToConvert.GenericTypeArguments[0]);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }


        private class NullableStructConverter<T> : JsonConverter<T?> where T : struct
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
                JsonSerializer.Serialize(writer, value, serializeOpts);
            }


        }
    }
}
