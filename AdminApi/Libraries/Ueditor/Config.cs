using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;


namespace AdminApi.Libraries.Ueditor
{
    /// <summary>
    /// Config 的摘要说明
    /// </summary>
    public static class Config
    {
        private readonly static bool noCache = true;
        private static JObject BuildItems()
        {
            var path = IO.Path.WebRootPath() + "/ueditor/config.json";
            var json = File.ReadAllText(path);
            return JObject.Parse(json);
        }

        public static JObject Items
        {
            get
            {
                if (noCache || _Items == null)
                {
                    _Items = BuildItems();
                }
                return _Items;
            }
        }
        private static JObject _Items;


        public static T GetValue<T>(string key)
        {
            return Items[key].Value<T>();
        }

        public static String[] GetStringList(string key)
        {
            return Items[key].Select(x => x.Value<String>()).ToArray();
        }

        public static String GetString(string key)
        {
            return GetValue<String>(key);
        }

        public static int GetInt(string key)
        {
            return GetValue<int>(key);
        }
    }

}