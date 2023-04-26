using AdminAPP.Libraries.JsonConverter;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AdminAPP.Libraries
{
    public class JsonHelper
    {

        private static JsonSerializerOptions objectToJsonOptions;

        private static JsonSerializerOptions jsonToObjectOptions;


        static JsonHelper()
        {
            objectToJsonOptions = new()
            {
                //关闭默认转义
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,

                //启用驼峰格式
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            objectToJsonOptions.Converters.Add(new DateTimeConverter());
            objectToJsonOptions.Converters.Add(new DateTimeOffsetConverter());
            objectToJsonOptions.Converters.Add(new LongConverter());


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



        /// <summary>
        /// 通过 Key 获取 Value
        /// </summary>
        /// <returns></returns>
        public static string? GetValueByKey(string json, string key)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                var jsonElement = doc.RootElement.Clone();

                return jsonElement.GetProperty(key).GetString();
            }
            catch
            {
                return null;
            }
        }



        /// <summary> 
        /// 对象 转 Json
        /// </summary> 
        /// <param name="obj">对象</param> 
        /// <returns>JSON格式的字符串</returns> 
        public static string ObjectToJson(object obj)
        {
            return JsonSerializer.Serialize(obj, objectToJsonOptions);
        }



        /// <summary> 
        /// Json 转 对象
        /// </summary> 
        /// <typeparam name="T">类型</typeparam> 
        /// <param name="jsonText">JSON文本</param> 
        /// <returns>指定类型的对象</returns> 
        public static T JsonToObject<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, jsonToObjectOptions)!;
        }



        /// <summary>
        /// 没有 Key 的 Json 转 List<JToken>
        /// </summary>
        /// <param name="strJson"></param>
        /// <returns></returns>
        public static JsonNode? JsonToArrayList(string json)
        {
            var jsonNode = JsonNode.Parse(json);

            return jsonNode;
        }

    }
}
