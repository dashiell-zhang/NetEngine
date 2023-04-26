using AdminAPP.Libraries.JsonConverter;
using System.Net.Http.Json;
using System.Text.Json;

namespace AdminAPP.Libraries
{
    public static class HttpClientExtension
    {

        private static JsonSerializerOptions jsonToObjectOptions;

        static HttpClientExtension()
        {
            jsonToObjectOptions = new()
            {
                //启用大小写不敏感
                PropertyNameCaseInsensitive = true
            };
            jsonToObjectOptions.Converters.Add(new LongConverter());
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
