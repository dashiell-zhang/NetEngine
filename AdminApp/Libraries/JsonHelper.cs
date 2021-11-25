using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdminApp.Libraries
{
    public class JsonHelper
    {


        /// <summary>
        /// 通过 Key 获取 Value
        /// </summary>
        /// <returns></returns>
        public static string GetValueByKey(string json, string key)
        {
            try
            {
                JObject jo = (JObject)JsonConvert.DeserializeObject(json);

                return jo[key].ToString();
            }
            catch
            {
                throw new Exception(json);
            }
        }




        /// <summary> 
        /// 对象 转 Json
        /// </summary> 
        /// <param name="obj">对象</param> 
        /// <returns>JSON格式的字符串</returns> 
        public static string ObjectToJson(object obj)
        {
            try
            {
                JsonSerializerSettings jset = new JsonSerializerSettings();
                jset.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                jset.Converters.Add(new IsoDateTimeConverter { DateTimeFormat = "yyyy'/'MM'/'dd' 'HH':'mm':'ss" });

                //设置属性名为首字母小写得驼峰形式
                jset.ContractResolver = new CamelCasePropertyNamesContractResolver();

                return JsonConvert.SerializeObject(obj, jset).ToString();
            }
            catch (Exception ex)
            {
                throw new Exception("JSONHelper.ObjectToJSON(): " + ex.Message);
            }
        }




        /// <summary> 
        /// Json 转 对象
        /// </summary> 
        /// <typeparam name="T">类型</typeparam> 
        /// <param name="jsonText">JSON文本</param> 
        /// <returns>指定类型的对象</returns> 
        public static T JsonToObject<T>(string jsonText)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(jsonText.Replace("undefined", "null"));
            }
            catch (Exception ex)
            {
                throw new Exception("JSONHelper.JSONToObject(): " + ex.Message);
            }
        }




        /// <summary>
        /// 没有 Key 的 Json 转 List<JToken>
        /// </summary>
        /// <param name="strJson"></param>
        /// <returns></returns>
        public static List<JToken> JsonToArrayList(string strJson)
        {
            return ((JArray)JsonConvert.DeserializeObject(strJson)).ToList();
        }
    }
}
