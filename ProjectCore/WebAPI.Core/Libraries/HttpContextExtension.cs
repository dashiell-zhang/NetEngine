﻿using Microsoft.IdentityModel.JsonWebTokens;
using Shared.Model;
using System.IO.Compression;
using System.Text;

namespace WebAPI.Core.Libraries
{

    /// <summary>
    /// HttpContext扩展方法
    /// </summary>
    public static class HttpContextExtension
    {


        /// <summary>
        /// 获取当前HttpContext
        /// </summary>
        /// <param name="httpContext"></param>
        /// <returns></returns>
        public static HttpContext Current(this HttpContext httpContext)
        {
            httpContext.Request.Body.Position = 0;
            return httpContext;
        }



        /// <summary>
        /// 获取 IP 信息
        /// </summary>
        /// <param name="httpContext"></param>
        /// <returns></returns>
        public static string GetRemoteIP(this HttpContext httpContext)
        {
            string[] headerKeys = { "x-original-forwarded-for", "x-forwarded-for" };

            foreach (var headerKey in headerKeys)
            {
                var headerValue = httpContext.Request.Headers
                    .FirstOrDefault(h => h.Key.Equals(headerKey, StringComparison.OrdinalIgnoreCase)).Value.ToString();

                if (!string.IsNullOrWhiteSpace(headerValue))
                {
                    var ip = headerValue.Split(',', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
                    if (!string.IsNullOrWhiteSpace(ip))
                    {
                        return ip;
                    }
                }
            }

            if (httpContext.Connection.RemoteIpAddress == null)
            {
                throw new Exception("RemoteIpAddress 为 null 无法处理");
            }

            var remoteIp = httpContext.Connection.RemoteIpAddress.ToString();

            if (httpContext.Connection.RemoteIpAddress.IsIPv4MappedToIPv6)
            {
                remoteIp = httpContext.Connection.RemoteIpAddress.MapToIPv4().ToString();
            }

            return remoteIp;
        }



        /// <summary>
        /// 获取完整URL信息
        /// </summary>
        /// <returns></returns>
        public static string GetURL(this HttpContext httpContext)
        {
            return httpContext.GetBaseURL() + $"{httpContext.Request.Path}{httpContext.Request.QueryString}";
        }



        /// <summary>
        /// 获取基础URL信息
        /// </summary>
        /// <returns></returns>
        public static string GetBaseURL(this HttpContext httpContext)
        {
            var url = $"{httpContext.Request.Scheme}://{httpContext.Request.Host.Host}";

            if (httpContext.Request.Host.Port != null)
            {
                url += $":{httpContext.Request.Host.Port}";
            }

            return url;
        }



        /// <summary>
        /// 获取RequestBody中的内容
        /// </summary>
        public static string GetRequestBody(this HttpContext httpContext)
        {
            httpContext.Request.Body.Position = 0;

            var requestContent = "";

            var contentEncoding = httpContext.Request.Headers.ContentEncoding.FirstOrDefault();

            if (contentEncoding != null && contentEncoding.Equals("gzip", StringComparison.OrdinalIgnoreCase))
            {
                using Stream requestBody = new MemoryStream();
                httpContext.Request.Body.CopyTo(requestBody);
                httpContext.Request.Body.Position = 0;

                requestBody.Position = 0;

                using GZipStream decompressedStream = new(requestBody, CompressionMode.Decompress);
                using StreamReader sr = new(decompressedStream, Encoding.UTF8);
                requestContent = sr.ReadToEnd();
            }
            else
            {
                using Stream requestBody = new MemoryStream();
                httpContext.Request.Body.CopyTo(requestBody);
                httpContext.Request.Body.Position = 0;

                requestBody.Position = 0;

                using StreamReader requestReader = new(requestBody);
                requestContent = requestReader.ReadToEnd();
            }

            return requestContent;
        }



        /// <summary>
        /// 获取Http请求中的全部参数
        /// </summary>
        public static Dictionary<string, string> GetParameters(this HttpContext httpContext)
        {

            var context = httpContext;

            Dictionary<string, string> parameters = [];

            if (context.Request.Method == "POST")
            {
                string body = httpContext.GetRequestBody();

                if (!string.IsNullOrEmpty(body))
                {
                    parameters.Add("body", body);
                }
                else if (context.Request.HasFormContentType)
                {
                    var fromlist = context.Request.Form.OrderBy(t => t.Key).ToList();

                    foreach (var fm in fromlist)
                    {
                        parameters.Add(fm.Key, fm.Value.ToString());
                    }
                }
            }
            else if (context.Request.Method == "GET")
            {
                var queryList = context.Request.Query.ToList();

                foreach (var query in queryList)
                {
                    parameters.Add(query.Key, query.Value.ToString());
                }
            }

            return parameters;
        }



        /// <summary>
        /// 获取Http请求中的JsonWebToken
        /// </summary>
        /// <param name="httpContext"></param>
        /// <returns></returns>
        public static JsonWebToken GetJsonWebToken(this HttpContext httpContext)
        {
            var authorizationStr = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

            return new JsonWebToken(authorizationStr);

        }

    }
}
