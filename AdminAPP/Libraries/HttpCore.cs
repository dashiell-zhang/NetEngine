using System.Net.Http.Json;
using System.Text.Json;

namespace AdminAPP.Libraries
{
    public static class HttpCore
    {



        public static Task<TValue?> GetFromJsonAsync<TValue>(this HttpClient client, string requestUri)
        {
            JsonSerializerOptions jsonSerializerOptions = new();

            jsonSerializerOptions.Converters.Add(new Json.DateTimeConverter());
            jsonSerializerOptions.Converters.Add(new Json.DateTimeNullConverter());
            jsonSerializerOptions.Converters.Add(new Json.LongConverter());
            jsonSerializerOptions.PropertyNameCaseInsensitive = true;

            return client.GetFromJsonAsync<TValue>(requestUri, jsonSerializerOptions);
        }



        public static TValue? ReadAsEntityAsync<TValue>(this HttpContent httpContent)
        {
            JsonSerializerOptions jsonSerializerOptions = new();

            jsonSerializerOptions.Converters.Add(new Json.DateTimeConverter());
            jsonSerializerOptions.Converters.Add(new Json.DateTimeNullConverter());
            jsonSerializerOptions.Converters.Add(new Json.LongConverter());
            jsonSerializerOptions.PropertyNameCaseInsensitive = true;

            var result = httpContent.ReadAsStringAsync().Result;

            return JsonSerializer.Deserialize<TValue>(result, jsonSerializerOptions);
        }

    }
}
