using System;
using System.Collections.Generic;
using System.Linq;

namespace Common
{
    public class DnsHelper
    {

        private static readonly string ServerUrl = "https://223.5.5.5/resolve";


        /// <summary>
        /// 获取指定域名的 Text 记录
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        public static List<string> GetDomainText(string domain)
        {
            string key = CryptoHelper.GetMd5("dns:" + domain + ":txt");

            string retStr = CacheHelper.GetString(key);

            if (string.IsNullOrEmpty(retStr))
            {
                string url = ServerUrl + "?name=" + domain + "&type=TXT";
                retStr = HttpHelper.Get(url);

                CacheHelper.SetString(key, retStr, TimeSpan.FromMinutes(10));
            }

            var retData = Json.JsonHelper.JsonToObject<DnsReturn>(retStr);

            return retData.Answer!.ToList().Select(t => t.Data!.Replace("\"", "")).ToList();
        }



        /// <summary>
        /// 获取指定域名的 A 记录
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        public static List<string> GetDomainA(string domain)
        {
            string key = CryptoHelper.GetMd5("dns:" + domain + ":a");

            string retStr = CacheHelper.GetString(key);

            if (string.IsNullOrEmpty(retStr))
            {
                string url = ServerUrl + "?name=" + domain + "&type=A";
                retStr = HttpHelper.Get(url);

                CacheHelper.SetString(key, retStr, TimeSpan.FromMinutes(10));
            }

            var retData = Json.JsonHelper.JsonToObject<DnsReturn>(retStr);

            return retData.Answer!.ToList().Where(t => t.Type == 1).Select(t => t.Data!.Replace("\"", "")).ToList();
        }



        /// <summary>
        /// 获取指定域名的 AAAA 记录
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        public static List<string> GetDomainAAAA(string domain)
        {
            string key = CryptoHelper.GetMd5("dns:" + domain + ":aaaa");

            string retStr = CacheHelper.GetString(key);

            if (string.IsNullOrEmpty(retStr))
            {
                string url = ServerUrl + "?name=" + domain + "&type=AAAA";
                retStr = HttpHelper.Get(url);

                CacheHelper.SetString(key, retStr, TimeSpan.FromMinutes(10));
            }

            var retData = Json.JsonHelper.JsonToObject<DnsReturn>(retStr);


            return retData.Answer!.ToList().Where(t => t.Type == 28).Select(t => t.Data!.Replace("\"", "")).ToList();
        }



        private class DnsReturn
        {
            public int Status { get; set; }

            public bool TC { get; set; }

            public bool RD { get; set; }

            public bool RA { get; set; }

            public bool AD { get; set; }

            public bool CD { get; set; }

            public DomainTextRetQuestion? Question { get; set; }

            public class DomainTextRetQuestion
            {
                public string? Name { get; set; }

                public int Type { get; set; }
            }

            public DomainTextRetAnswer[]? Answer { get; set; }

            public class DomainTextRetAnswer
            {
                public string? Name { get; set; }

                public int TTL { get; set; }

                public int Type { get; set; }

                public string? Data { get; set; }
            }

        }


    }
}
