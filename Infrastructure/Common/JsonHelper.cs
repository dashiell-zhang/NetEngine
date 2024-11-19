using Common.JsonConverter;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common
{
    public class JsonHelper
    {

        private static readonly JsonSerializerOptions objectToJsonOptions;

        private static readonly JsonSerializerOptions jsonToObjectOptions;

        private static readonly JsonSerializerOptions cloneObjectOptions;


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
            objectToJsonOptions.Converters.Add(new StringConverter());


            jsonToObjectOptions = new()
            {
                //启用大小写不敏感
                PropertyNameCaseInsensitive = true
            };
            jsonToObjectOptions.Converters.Add(new DateTimeConverter());
            jsonToObjectOptions.Converters.Add(new DateTimeOffsetConverter());
            jsonToObjectOptions.Converters.Add(new LongConverter());
            jsonToObjectOptions.Converters.Add(new StringConverter());


            #region cloneObjectOptions

            cloneObjectOptions = new()
            {
                ReferenceHandler = ReferenceHandler.Preserve,   //解决循环依赖
                DefaultIgnoreCondition = JsonIgnoreCondition.Never, //屏蔽 JsonIgnore 配置
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,  //关闭默认转义
            };
            cloneObjectOptions.Converters.Add(new LongConverter());

            #endregion
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
                return doc.RootElement.GetProperty(key).ToString();
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
        /// 对象 克隆 Json
        /// </summary> 
        /// <param name="obj">对象</param> 
        /// <returns>JSON格式的字符串</returns> 
        public static string ObjectCloneJson(object obj)
        {
            return JsonSerializer.Serialize(obj, cloneObjectOptions);
        }



        /// <summary> 
        /// Json 克隆 对象
        /// </summary> 
        /// <typeparam name="T">类型</typeparam> 
        /// <param name="jsonText">JSON文本</param> 
        /// <returns>指定类型的对象</returns> 
        public static T JsonCloneObject<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, cloneObjectOptions)!;
        }



        /// <summary>
        /// 对象 克隆 对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T Clone<T>(T obj) where T : class, new()
        {
            var json = JsonSerializer.Serialize(obj, cloneObjectOptions);
            return JsonSerializer.Deserialize<T>(json, cloneObjectOptions)!;
        }


    }
}
