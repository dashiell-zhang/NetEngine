using AdminAPP.Libraries.JsonConverter;
using System.Net.Http.Json;
using System.Text.Json;

namespace AdminAPP.Libraries
{
    public static class HttpCore
    {

        private static JsonSerializerOptions jsonToObjectOptions;

        static HttpCore()
        {
            jsonToObjectOptions = new()
            {
                //启用大小写不敏感
                PropertyNameCaseInsensitive = true
            };
            jsonToObjectOptions.Converters.Add(new DateTimeConverter());
            jsonToObjectOptions.Converters.Add(new DateTimeOffsetConverter());
            jsonToObjectOptions.Converters.Add(new LongConverter());
            jsonToObjectOptions.Converters.Add(new NullableStructConverterFactory());
        }


        public static Task<TValue?> GetFromJsonAsync<TValue>(this HttpClient client, string requestUri)
        {
            return client.GetFromJsonAsync<TValue>(requestUri, jsonToObjectOptions);
        }


        public static TValue? ReadAsEntityAsync<TValue>(this HttpContent httpContent)
        {
            var result = httpContent.ReadAsStringAsync().Result;

            return JsonSerializer.Deserialize<TValue>(result, jsonToObjectOptions);
        }

    }
}
