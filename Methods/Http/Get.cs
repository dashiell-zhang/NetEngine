using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Methods.Http
{
    public class Get
    {

        public static string Run(string Url)
        {

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url);
            request.Method = "GET";
            request.ContentType = "text/html;charset=UTF-8";
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
    }
}
