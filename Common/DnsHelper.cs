using System;
using System.Collections.Generic;
using System.Linq;

namespace Common
{
    public class DnsHelper
    {



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
                string url = "https://dns.alidns.com/resolve?name=" + domain + "&type=TXT";
                retStr = HttpHelper.Get(url);

                CacheHelper.SetString(key, retStr, TimeSpan.FromMinutes(10));
            }

            var retData = Json.JsonHelper.JsonToObject<DnsReturn>(retStr);

            return retData.Answer.ToList().Select(t => t.data.Replace("\"", "")).ToList();
        }



        private class DnsReturn
        {
            public int Status { get; set; }

            public bool TC { get; set; }

            public bool RD { get; set; }

            public bool RA { get; set; }

            public bool AD { get; set; }

            public bool CD { get; set; }

            public DomainTextRetQuestion Question { get; set; }

            public class DomainTextRetQuestion
            {
                public string name { get; set; }
                public int type { get; set; }
            }

            public DomainTextRetAnswer[] Answer { get; set; }

            public class DomainTextRetAnswer
            {
                public string name { get; set; }
                public int TTL { get; set; }
                public int type { get; set; }
                public string data { get; set; }
            }

        }





    }
}
