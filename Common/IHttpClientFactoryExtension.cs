using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Common
{

    /// <summary>
    /// 扩展 Http客户端工厂接口,集成常用方法
    /// </summary>
    public static class IHttpClientFactoryExtension
    {



        /// <summary>
        /// Get方式获取远程资源
        /// </summary>
        /// <param name="url">请求地址</param>
        /// <param name="headers">自定义Header集合</param>
        /// <param name="httpClientName">httpClient名称</param>
        /// <returns></returns>
        public static string Get(this IHttpClientFactory httpClientFactory, string url, Dictionary<string, string>? headers = default, string? httpClientName = "")
        {

            var client = httpClientFactory.CreateClient(httpClientName!);

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
        /// Post Json或XML 数据到指定url
        /// </summary>
        /// <param name="url">Url</param>
        /// <param name="data">数据</param>
        /// <param name="type">json,xml</param>
        /// <param name="headers">自定义Header集合</param>
        /// <param name="httpClientName">httpClient名称</param>
        /// <returns></returns>
        public static string Post(this IHttpClientFactory httpClientFactory, string url, string data, string type, Dictionary<string, string>? headers = default, string? httpClientName = "")
        {

            var client = httpClientFactory.CreateClient(httpClientName!);

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
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }
            else if (type == "xml")
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
            }

            content.Headers.ContentType!.CharSet = "utf-8";

            using var httpResponse = client.PostAsync(url, content);
            return httpResponse.Result.Content.ReadAsStringAsync().Result;
        }




        /// <summary>
        /// Delete 方式发出请求
        /// </summary>
        /// <param name="url">Url</param>
        /// <param name="headers">自定义Header集合</param>
        /// <param name="httpClientName">httpClient名称</param>
        /// <returns></returns>
        public static string Delete(this IHttpClientFactory httpClientFactory, string url, Dictionary<string, string>? headers = default, string? httpClientName = "")
        {

            var client = httpClientFactory.CreateClient(httpClientName!);

            if (headers != default)
            {
                foreach (var header in headers)
                {
                    client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            using var httpResponse = client.DeleteAsync(url);
            return httpResponse.Result.Content.ReadAsStringAsync().Result;
        }




        /// <summary>
        /// Post Json或XML 数据到指定url,异步执行
        /// </summary>
        /// <param name="url">Url</param>
        /// <param name="data">数据</param>
        /// <param name="type">json,xml</param>
        /// <param name="headers">自定义Header集合</param>
        /// <param name="httpClientName">httpClient名称</param>
        /// <returns></returns>
        public static async void PostAsync(this IHttpClientFactory httpClientFactory, string url, string data, string type, Dictionary<string, string>? headers = default, string? httpClientName = "")
        {
            await Task.Run(() =>
            {
                Post(httpClientFactory, url, data, type, headers, httpClientName);
            });
        }




        /// <summary>
        /// Post表单数据到指定url
        /// </summary>
        /// <param name="url"></param>
        /// <param name="formItems">Post表单内容</param>
        /// <param name="headers">自定义Header集合</param>
        /// <param name="httpClientName">httpClient名称</param>
        /// <returns></returns>
        public static string PostForm(this IHttpClientFactory httpClientFactory, string url, Dictionary<string, string> formItems, Dictionary<string, string>? headers = default, string? httpClientName = "")
        {

            var client = httpClientFactory.CreateClient(httpClientName!);

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
        /// <param name="httpClientName">httpClient名称</param>
        /// <returns></returns>
        public static string PostFormData(this IHttpClientFactory httpClientFactory, string url, List<PostFormDataItem> formItems, Dictionary<string, string>? headers = default, string? httpClientName = "")
        {

            var client = httpClientFactory.CreateClient(httpClientName!);

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
