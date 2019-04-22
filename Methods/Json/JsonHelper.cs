using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;

namespace Methods.Json
{
    public class JsonHelper
    {


        /// <summary>
        /// 通过 Key 获取 Value
        /// </summary>
        /// <returns></returns>
        public static string GetValueByKey(string json,string key)
        {
            JObject jo = (JObject)JsonConvert.DeserializeObject(json);

            return jo[key].ToString();
        }




        /// <summary>
        /// DataRow转JSON
        /// </summary>
        /// <param name="row">DataRow</param>
        /// <returns>JSON格式对象</returns>
        public static object DataRowToJSON(DataRow row)
        {
            Dictionary<string, object> dataList = new Dictionary<string, object>();
            foreach (DataColumn column in row.Table.Columns)
            {
                dataList.Add(column.ColumnName, row[column]);
            }

            return ObjectToJSON(dataList);
        }

        /// <summary>
        /// DataRow转对象，泛型方法
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="row">DataRow</param>
        /// <returns>JSON格式对象</returns>
        public static T DataRowToObject<T>(DataRow row)
        {
            return JSONToObject<T>(DataRowToJSON(row).ToString());
        }

        /// <summary>
        /// DataRow转对象，泛型方法
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="table">DataTable</param>
        /// <returns>JSON格式对象</returns>
        public static List<T> DataTableToList<T>(DataTable table)
        {
            return JSONToList<T>(DataTableToJSON(table).ToString());
        }
        /// <summary>
        /// DataRow转对象，泛型方法
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="jsonText">JSON文本</param> 
        /// <returns>JSON格式对象</returns>
        public static List<T> JSONToList<T>(string jsonText)
        {
            return JSONToObject<List<T>>(jsonText);
        }

        /// <summary> 
        /// 对象转JSON 
        /// </summary> 
        /// <param name="obj">对象</param> 
        /// <returns>JSON格式的字符串</returns> 
        public static string ObjectToJSON(object obj)
        {
            try
            {
                JsonSerializerSettings jset = new JsonSerializerSettings();
                jset.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                jset.Converters.Add(new IsoDateTimeConverter { DateTimeFormat = "yyyy'/'MM'/'dd' 'HH':'mm':'ss" });
                return JsonConvert.SerializeObject(obj, jset).ToString();
            }
            catch (Exception ex)
            {
                throw new Exception("JSONHelper.ObjectToJSON(): " + ex.Message);
            }
        }
        /// <summary> 
        /// 数据表转JSON 
        /// </summary> 
        /// <param name="dataTable">数据表</param> 
        /// <returns>JSON字符串</returns> 
        public static object DataTableToJSON(DataTable dataTable)
        {
            return ObjectToJSON(dataTable);
        }

        /// <summary> 
        /// JSON文本转对象,泛型方法 
        /// </summary> 
        /// <typeparam name="T">类型</typeparam> 
        /// <param name="jsonText">JSON文本</param> 
        /// <returns>指定类型的对象</returns> 
        public static T JSONToObject<T>(string jsonText)
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
        /// JSON文本转对象 
        /// </summary> 
        /// <param name="jsonText">JSON文本</param> 
        /// <param name="type">类型</param>
        /// <returns>指定类型的对象</returns> 
        public static object JSONToObject(string jsonText, Type type)
        {
            try
            {
                return JsonConvert.DeserializeObject(jsonText.Replace("undefined", "null"), type);
            }
            catch (Exception ex)
            {
                throw new Exception("JSONHelper.JSONToObject(): " + ex.Message);
            }
        }


        /// <summary>
        /// [{column1:1,column2:2,column3:3},{column1:1,column2:2,column3:3}]
        /// </summary> 
        /// <param name="strJson">Json字符串</param> 
        /// <returns>DataTable</returns>
        public static DataTable JSONToDataTable(string strJson)
        {
            return JsonConvert.DeserializeObject(strJson, typeof(DataTable)) as DataTable;
        }
    }
}
