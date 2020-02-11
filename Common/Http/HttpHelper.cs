using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Common.Http
{

    /// <summary>
    /// 常用Http操作类集合
    /// </summary>
    public static class HttpHelper
    {


        /// <summary>
        /// Get方式获取远程资源
        /// </summary>
        /// <param name="Url"></param>
        /// <returns></returns>
        public static string Get(string Url)
        {

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url);
            request.Method = "GET";
            request.Accept = "*/*";
            request.UserAgent = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; Trident/6.0)";
            request.Headers.Add("Accept-Language", "zh-CN,zh;q=0.9");


            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            Stream myResponseStream = response.GetResponseStream();
            StreamReader myStreamReader = new StreamReader(myResponseStream, Encoding.UTF8);
            string retString = myStreamReader.ReadToEnd();
            myStreamReader.Close();
            myResponseStream.Close();
            var obj = new
            {
                Result = retString,
                IsSuccess = true
            };
            return retString;
        }



        /// <summary>
        /// Post数据到指定url
        /// </summary>
        /// <param name="Url">Url</param>
        /// <param name="Data">数据</param>
        /// <param name="type">form,data,json,xml</param>
        /// <returns></returns>
        public static string Post(string Url, string Data, string type)
        {

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(Url);
            byte[] requestBytes = System.Text.Encoding.UTF8.GetBytes(Data);
            req.Method = "POST";
            if (type == "form")
            {
                req.ContentType = "application/x-www-form-urlencoded";
            }
            else if (type == "data")
            {
                req.ContentType = "multipart/form-data";
            }
            else if (type == "json")
            {
                req.ContentType = "application/json";
            }
            else if (type == "xml")
            {
                req.ContentType = "text/xml";
            }

            req.ContentLength = requestBytes.Length;
            Stream requestStream = req.GetRequestStream();
            requestStream.Write(requestBytes, 0, requestBytes.Length);
            requestStream.Close();

            HttpWebResponse res = (HttpWebResponse)req.GetResponse();
            StreamReader sr = new StreamReader(res.GetResponseStream(), System.Text.Encoding.UTF8);
            string PostJie = sr.ReadToEnd();
            sr.Close();
            res.Close();

            return PostJie;
        }


        /// <summary>
        /// Post数据到指定url,异步执行
        /// </summary>
        /// <param name="Url">Url</param>
        /// <param name="Data">数据</param>
        /// <param name="type">form,data,json,xml</param>
        /// <returns></returns>
        public async static void PostAsync(string Url, string Data, string type)
        {

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(Url);
            byte[] requestBytes = System.Text.Encoding.UTF8.GetBytes(Data);
            req.Method = "POST";
            if (type == "form")
            {
                req.ContentType = "application/x-www-form-urlencoded";
            }
            else if (type == "data")
            {
                req.ContentType = "multipart/form-data";
            }
            else if (type == "json")
            {
                req.ContentType = "application/json";
            }
            else if (type == "xml")
            {
                req.ContentType = "text/xml";
            }

            req.ContentLength = requestBytes.Length;
            Stream requestStream = await req.GetRequestStreamAsync();
            requestStream.Write(requestBytes, 0, requestBytes.Length);
            requestStream.Close();

            HttpWebResponse res = (HttpWebResponse)req.GetResponse();
            StreamReader sr = new StreamReader(res.GetResponseStream(), System.Text.Encoding.UTF8);
            string PostJie = sr.ReadToEnd();
            sr.Close();
            res.Close();

        }
    }
}
