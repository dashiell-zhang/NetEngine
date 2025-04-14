using Admin.App.Libraries.JsonConverters;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Admin.App.Libraries
{
    public class JsonHelper
    {

        public static readonly JsonSerializerOptions SerializeOpts;

        public static readonly JsonSerializerOptions DeserializeOpts;


        static JsonHelper()
        {
            SerializeOpts = new()
            {
                //关闭默认转义
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,

                //启用驼峰格式
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            SerializeOpts.Converters.Add(new DateTimeOffsetConverter());
            SerializeOpts.Converters.Add(new LongConverter());


            DeserializeOpts = new()
            {
                //启用大小写不敏感
                PropertyNameCaseInsensitive = true
            };
            DeserializeOpts.Converters.Add(new DateTimeOffsetConverter());
            DeserializeOpts.Converters.Add(new LongConverter());
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
            return JsonSerializer.Serialize(obj, SerializeOpts);
        }



        /// <summary> 
        /// Json 转 对象
        /// </summary> 
        /// <typeparam name="T">类型</typeparam> 
        /// <param name="jsonText">JSON文本</param> 
        /// <returns>指定类型的对象</returns> 
        public static T JsonToObject<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, DeserializeOpts)!;
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
