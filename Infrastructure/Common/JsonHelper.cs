using Common.JsonConverters;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common;

public class JsonHelper
{

    public static readonly JsonSerializerOptions SerializeOpts;

    public static readonly JsonSerializerOptions DeserializeOpts;

    private static readonly JsonSerializerOptions cloneOpts;


    static JsonHelper()
    {
        SerializeOpts = new()
        {
            //关闭默认转义
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,

            //启用驼峰格式
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        SerializeOpts.Converters.Add(new DateTimeConverter());
#if !BROWSER
        SerializeOpts.Converters.Add(new DateTimeOffsetConverter());
#else
        SerializeOpts.Converters.Add(new DateTimeOffsetConverter(DateTimeOffset.Now.Offset));
#endif
        SerializeOpts.Converters.Add(new LongConverter());


        DeserializeOpts = new()
        {
            //启用大小写不敏感
            PropertyNameCaseInsensitive = true
        };
        DeserializeOpts.Converters.Add(new DateTimeConverter());
#if !BROWSER
        DeserializeOpts.Converters.Add(new DateTimeOffsetConverter());
#else
         DeserializeOpts.Converters.Add(new DateTimeOffsetConverter(DateTimeOffset.Now.Offset));
#endif
        DeserializeOpts.Converters.Add(new LongConverter());


        #region cloneObjectOptions

        cloneOpts = new()
        {
            ReferenceHandler = ReferenceHandler.Preserve,   //解决循环依赖
            DefaultIgnoreCondition = JsonIgnoreCondition.Never, //屏蔽 JsonIgnore 配置
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,  //关闭默认转义
        };
        cloneOpts.Converters.Add(new LongConverter());

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
    /// 对象 克隆 Json
    /// </summary> 
    /// <param name="obj">对象</param> 
    /// <returns>JSON格式的字符串</returns> 
    public static string ObjectCloneJson(object obj)
    {
        return JsonSerializer.Serialize(obj, cloneOpts);
    }



    /// <summary> 
    /// Json 克隆 对象
    /// </summary> 
    /// <typeparam name="T">类型</typeparam> 
    /// <param name="jsonText">JSON文本</param> 
    /// <returns>指定类型的对象</returns> 
    public static T JsonCloneObject<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, cloneOpts)!;
    }



    /// <summary>
    /// 对象 克隆 对象
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static T Clone<T>(T obj) where T : class, new()
    {
        var json = JsonSerializer.Serialize(obj, cloneOpts);
        return JsonSerializer.Deserialize<T>(json, cloneOpts)!;
    }


}
