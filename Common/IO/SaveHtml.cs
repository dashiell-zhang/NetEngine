using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Common.IO
{
    public class SaveHtml
    {
        /// <summary>
        /// 生成静态页需要传入目标网址和本地文件保存路径
        /// </summary>
        /// <param name="WebUrl"></param>
        /// <param name="SaveUrl"></param>
        public static void Run(string WebUrl, string SaveUrl)
        {
            string strHtml;
            StreamReader sr = null; //用来读取流       
            StreamWriter sw = null; //用来写文件     
            Encoding code = Encoding.GetEncoding("utf-8"); //定义编码      
                                                           //构造web请求，发送请求，获取响应     
            WebRequest HttpWebRequest = null;
            WebResponse HttpWebResponse = null;
            HttpWebRequest = WebRequest.Create(WebUrl);
            HttpWebResponse = HttpWebRequest.GetResponse();        //获得流   
            sr = new StreamReader(HttpWebResponse.GetResponseStream(), code);
            strHtml = sr.ReadToEnd();        //写入文件      
            try
            {
                sw = new StreamWriter(SaveUrl, false, code);
                sw.Write(strHtml);
                sw.Flush();
            }
            catch
            {

            }
            finally
            {
                sw.Close();
            }
        }

    }
}
