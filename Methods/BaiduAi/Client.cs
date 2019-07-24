using System;
using System.Collections.Generic;
using System.Text;

namespace Methods.BaiduAi
{
    public class Client
    {
        public static Baidu.Aip.ImageSearch.ImageSearch ImageSearch()
        {
            var API_KEY = "tmXSUIg7prvaLswMwvynXGMw";

            var SECRET_KEY = "ZXKtkPihFxt6UxAZhev1KTw74Gf4Koih ";

            var client = new Baidu.Aip.ImageSearch.ImageSearch(API_KEY, SECRET_KEY);
            client.Timeout = 60000;  // 修改超时时间

            return client;
        }





        public static Baidu.Aip.ImageClassify.ImageClassify ImageClassify()
        {
            var API_KEY = "tmXSUIg7prvaLswMwvynXGMw";

            var SECRET_KEY = "ZXKtkPihFxt6UxAZhev1KTw74Gf4Koih ";

            var client = new Baidu.Aip.ImageClassify.ImageClassify(API_KEY, SECRET_KEY);
            client.Timeout = 60000;  // 修改超时时间

            return client;
        }
    }
}
