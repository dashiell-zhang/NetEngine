﻿using System.Reflection;
using System.Text;
using System.Web;

namespace Admin.App.Libraries
{
    /// <summary>
    /// 常用Http操作类集合
    /// </summary>
    public class HttpHelper
    {


        /// <summary>
        /// Get方式获取远程资源
        /// </summary>
        /// <param name="url">请求地址</param>
        /// <param name="headers">自定义Header集合</param>
        /// <returns></returns>
        public static string Get(string url, Dictionary<string, string>? headers = default)
        {
            using HttpClientHandler handler = new();
            using HttpClient client = new(handler);
            client.DefaultRequestVersion = new("2.0");

            if (headers != default)
            {
                foreach (var header in headers)
                {
                    client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            using var httpResponse = client.GetStringAsync(url);
            return httpResponse.Result;
        }




        /// <summary>
        /// Model对象转换为Uri网址参数形式
        /// </summary>
        /// <param name="obj">Model对象</param>
        /// <param name="url">前部分网址</param>
        /// <returns></returns>
        public static string ModelToUriParam(object obj, string url = "")
        {
            PropertyInfo[] propertis = obj.GetType().GetProperties();
            StringBuilder sb = new();
            sb.Append(url);
            sb.Append('?');
            foreach (var p in propertis)
            {
                var v = p.GetValue(obj, null);
                if (v == null)
                    continue;

                sb.Append(p.Name);
                sb.Append('=');
                sb.Append(HttpUtility.UrlEncode(v.ToString()));
                sb.Append('&');
            }
            sb.Remove(sb.Length - 1, 1);

            return sb.ToString();
        }




        /// <summary>
        /// Post Json或XML 数据到指定url
        /// </summary>
        /// <param name="url">Url</param>
        /// <param name="data">数据</param>
        /// <param name="type">json,xml</param>
        /// <param name="headers">自定义Header集合</param>
        /// <returns></returns>
        public static string Post(string url, string data, string type, Dictionary<string, string>? headers = default)
        {

            using HttpClientHandler handler = new();

            using HttpClient client = new(handler);
            client.DefaultRequestVersion = new("2.0");

            if (headers != default)
            {
                foreach (var header in headers)
                {
                    client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            using Stream dataStream = new MemoryStream(Encoding.UTF8.GetBytes(data));
            using HttpContent content = new StreamContent(dataStream);


            if (type == "json")
            {
                content.Headers.ContentType = new("application/json");
            }
            else if (type == "xml")
            {
                content.Headers.ContentType = new("text/xml");
            }

            content.Headers.ContentType!.CharSet = "utf-8";

            using var httpResponse = client.PostAsync(url, content);
            return httpResponse.Result.Content.ReadAsStringAsync().Result;
        }




        /// <summary>
        /// Post Json或XML 数据到指定url,异步执行
        /// </summary>
        /// <param name="url">Url</param>
        /// <param name="data">数据</param>
        /// <param name="type">json,xml</param>
        /// <param name="headers">自定义Header集合</param>
        /// <returns></returns>
        public static void PostAsync(string url, string data, string type, Dictionary<string, string>? headers = default)
        {
            Task.Run(() =>
            {
                Post(url, data, type, headers);
            });
        }




        /// <summary>
        /// Post表单数据到指定url
        /// </summary>
        /// <param name="url"></param>
        /// <param name="formItems">Post表单内容</param>
        /// <param name="headers">自定义Header集合</param>
        /// <param name="isSkipSslVerification">是否跳过SSL验证</param>
        /// <returns></returns>
        public static string PostForm(string url, Dictionary<string, string> formItems, Dictionary<string, string>? headers = default)
        {
            using HttpClientHandler handler = new();

            using HttpClient client = new(handler);
            client.DefaultRequestVersion = new("2.0");

            if (headers != default)
            {
                foreach (var header in headers)
                {
                    client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            using FormUrlEncodedContent formContent = new(formItems);
            formContent.Headers.ContentType!.CharSet = "utf-8";

            using var httpResponse = client.PostAsync(url, formContent);
            return httpResponse.Result.Content.ReadAsStringAsync().Result;
        }




        /// <summary>
        /// Post文件和数据到指定url
        /// </summary>
        /// <param name="url"></param>
        /// <param name="formItems">Post表单内容</param>
        /// <param name="headers">自定义Header集合</param>
        /// <param name="isSkipSslVerification">是否跳过SSL验证</param>
        /// <returns></returns>
        public static string PostFormData(string url, List<PostFormDataItem> formItems, Dictionary<string, string>? headers = default)
        {
            using HttpClientHandler handler = new();

            using HttpClient client = new(handler);
            client.DefaultRequestVersion = new("2.0");

            if (headers != default)
            {
                foreach (var header in headers)
                {
                    client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            string boundary = "----" + DateTime.UtcNow.Ticks.ToString("x");

            using MultipartFormDataContent formDataContent = new(boundary);
            foreach (var item in formItems)
            {
                if (item.IsFile)
                {
                    //上传文件
                    formDataContent.Add(new StreamContent(item.FileContent!), item.Key!, item.FileName!);
                }
                else
                {
                    //上传文本
                    formDataContent.Add(new StringContent(item.Value!), item.Key!);
                }
            }

            using var httpResponse = client.PostAsync(url, formDataContent);
            return httpResponse.Result.Content.ReadAsStringAsync().Result;
        }



        /// <summary>
        /// Post 提交 FromData 表单数据模型结构
        /// </summary>
        public class PostFormDataItem
        {

            /// <summary>
            /// 表单键，request["key"]
            /// </summary>
            public string? Key { set; get; }



            /// <summary>
            /// 表单值,上传文件时忽略，request["key"].value
            /// </summary>
            public string? Value { set; get; }



            /// <summary>
            /// 是否是文件
            /// </summary>
            public bool IsFile
            {
                get
                {
                    if (FileContent == null || FileContent.Length == 0)
                        return false;

                    if (FileContent != null && FileContent.Length > 0 && string.IsNullOrWhiteSpace(FileName))
                        throw new Exception("上传文件时 FileName 属性值不能为空");
                    return true;
                }
            }



            /// <summary>
            /// 上传的文件名
            /// </summary>
            public string? FileName { set; get; }



            /// <summary>
            /// 上传的文件内容
            /// </summary>
            public Stream? FileContent { set; get; }


        }
    }
}
