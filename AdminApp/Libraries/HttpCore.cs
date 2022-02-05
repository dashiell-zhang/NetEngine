using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace AdminApp.Libraries
{
    public static class HttpCore
    {



        public static Task<TValue?> GetFromJsonAsync<TValue>(this HttpClient client, string requestUri)
        {
            var jsonSerializerOptions = new JsonSerializerOptions();

            jsonSerializerOptions.Converters.Add(new Json.DateTimeConverter());
            jsonSerializerOptions.Converters.Add(new Json.DateTimeNullConverter());
            jsonSerializerOptions.Converters.Add(new Json.LongConverter());
            jsonSerializerOptions.PropertyNameCaseInsensitive = true;

            return client.GetFromJsonAsync<TValue>(requestUri, jsonSerializerOptions);
        }



        public static Task<HttpResponseMessage> DeleteAsJsonAsync<TValue>(this HttpClient client, string requestUri, TValue value)
        {
            HttpRequestMessage request = new()
            {
                Content = JsonContent.Create(value),
                Method = HttpMethod.Delete,
                RequestUri = new Uri(requestUri, UriKind.Relative)
            };

            return client.SendAsync(request);
        }


        public static TValue? ReadAsEntityAsync<TValue>(this HttpContent httpContent)
        {
            var jsonSerializerOptions = new JsonSerializerOptions();

            jsonSerializerOptions.Converters.Add(new Json.DateTimeConverter());
            jsonSerializerOptions.Converters.Add(new Json.DateTimeNullConverter());
            jsonSerializerOptions.Converters.Add(new Json.LongConverter());
            jsonSerializerOptions.PropertyNameCaseInsensitive = true;

            var result = httpContent.ReadAsStringAsync().Result;

            return JsonSerializer.Deserialize<TValue>(result, jsonSerializerOptions);
        }

    }
}
