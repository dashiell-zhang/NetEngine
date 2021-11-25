using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AdminApp.Libraries
{
    public static class HttpCore
    {


#pragma warning disable CS8632 // 只能在 "#nullable" 注释上下文内的代码中使用可为 null 的引用类型的注释。
        public static Task<TValue?> GetFromJsonAsync<TValue>(this HttpClient client, string? requestUri)
#pragma warning restore CS8632 // 只能在 "#nullable" 注释上下文内的代码中使用可为 null 的引用类型的注释。
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
            HttpRequestMessage request = new HttpRequestMessage
            {
                Content = JsonContent.Create(value),
                Method = HttpMethod.Delete,
                RequestUri = new Uri(requestUri, UriKind.Relative)
            };

            return client.SendAsync(request);
        }


        public static TValue ReadAsEntityAsync<TValue>(this HttpContent httpContent)
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
