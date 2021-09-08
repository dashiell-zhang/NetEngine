using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlazorCms.Libraries
{
    public static class HttpCore
    {


#pragma warning disable CS8632 // 只能在 "#nullable" 注释上下文内的代码中使用可为 null 的引用类型的注释。
        public static Task<TValue?> GetFromJsonAsync<TValue>(this HttpClient client, string? requestUri)
#pragma warning restore CS8632 // 只能在 "#nullable" 注释上下文内的代码中使用可为 null 的引用类型的注释。
        {
            var jsonSerializerOptions = new JsonSerializerOptions();

            jsonSerializerOptions.Converters.Add(new JsonConverter.DateTimeConverter());
            jsonSerializerOptions.Converters.Add(new JsonConverter.DateTimeNullConverter());
            jsonSerializerOptions.Converters.Add(new JsonConverter.LongConverter());
            jsonSerializerOptions.PropertyNameCaseInsensitive = true;

            return client.GetFromJsonAsync<TValue>(requestUri, jsonSerializerOptions);
        }

       
        public static TValue ReadAsClassAsync<TValue>(this HttpContent httpContent)
        {
            var jsonSerializerOptions = new JsonSerializerOptions();

            jsonSerializerOptions.Converters.Add(new JsonConverter.DateTimeConverter());
            jsonSerializerOptions.Converters.Add(new JsonConverter.DateTimeNullConverter());
            jsonSerializerOptions.Converters.Add(new JsonConverter.LongConverter());
            jsonSerializerOptions.PropertyNameCaseInsensitive = true;

            var s = httpContent.ReadAsStringAsync().Result;

            return JsonSerializer.Deserialize<TValue>(s, jsonSerializerOptions);

        }

    }
}
