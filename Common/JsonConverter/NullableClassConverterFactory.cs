using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common.JsonConverter
{

    public class NullableClassConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsClass && typeToConvert.Namespace != "System" && typeToConvert.Namespace != null;
        }

        public override System.Text.Json.Serialization.JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
        {
            return (System.Text.Json.Serialization.JsonConverter)Activator.CreateInstance(typeof(NullableClassConverter<>).MakeGenericType(type))!;
        }

        private class NullableClassConverter<T> : JsonConverter<T> where T : class
        {

            public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var tempOptions = new JsonSerializerOptions(options);

                var thisFactory = new NullableClassConverterFactory().ToString();
                tempOptions.Converters.Remove(options.Converters.FirstOrDefault(t => t.ToString() == thisFactory)!);

                using (var jsonDoc = JsonDocument.ParseValue(ref reader))
                {
                    var jsonText = jsonDoc.RootElement.GetRawText().Replace(" ", "").Replace("\n", "");

                    if (jsonText == "{}" || jsonText == "[]")
                    {
                        return null;
                    }
                    else
                    {
                        foreach (var item in typeToConvert.GetProperties())
                        {
                            if (item.PropertyType.IsClass && item.PropertyType.Namespace != "System" && item.CustomAttributes.Any(t => t.AttributeType.Name == "NullableAttribute"))
                            {
                                tempOptions.Converters.Add((System.Text.Json.Serialization.JsonConverter)Activator.CreateInstance(typeof(NullableClassConverter<>).MakeGenericType(item.PropertyType))!);
                            }
                        }

                        return JsonSerializer.Deserialize<T>(jsonText, tempOptions);
                    }
                }
            }


            public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, value, options);
            }

        }
    }

}
