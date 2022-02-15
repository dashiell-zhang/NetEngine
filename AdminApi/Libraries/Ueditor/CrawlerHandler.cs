﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace AdminApi.Libraries.Ueditor
{
    /// <summary>
    /// Crawler 的摘要说明
    /// </summary>
    public class CrawlerHandler : Handler
    {
        private string[]? Sources;
        private Crawler[]? Crawlers;

        public override string Process()
        {
            Sources = Http.HttpContext.Current().Request.Form["source[]"];
            if (Sources == null || Sources.Length == 0)
            {
                return WriteJson(new
                {
                    state = "参数错误：没有指定抓取源"
                });
            }
            Crawlers = Sources.Select(x => new Crawler(x).Fetch()).ToArray();
            return WriteJson(new
            {
                state = "SUCCESS",
                list = Crawlers.Select(x => new
                {
                    state = x.State,
                    source = x.SourceUrl,
                    url = x.ServerUrl
                })
            });
        }
    }

    public class Crawler
    {
        public string? SourceUrl { get; set; }
        public string? ServerUrl { get; set; }
        public string? State { get; set; }




        public Crawler(string sourceUrl)
        {
            SourceUrl = sourceUrl;
        }

        public Crawler Fetch()
        {
            if (!IsExternalIPAddress(SourceUrl!))
            {
                State = "INVALID_URL";
                return this;
            }


            using HttpClient client = new();
            client.DefaultRequestVersion = new Version("2.0");
            using var httpResponse = client.GetAsync(SourceUrl).Result;
            if (httpResponse.StatusCode != HttpStatusCode.OK)
            {
                State = "Url returns " + httpResponse.StatusCode;
                return this;
            }


            if (httpResponse.Content.Headers.ContentType?.MediaType?.Contains("image") == false)
            {
                State = "Url is not an image";
                return this;
            }
            ServerUrl = PathFormatter.Format(Path.GetFileName(SourceUrl!), Config.GetString("catcherPathFormat"));
            var savePath = IO.Path.WebRootPath() + ServerUrl;
            if (!Directory.Exists(Path.GetDirectoryName(savePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
            }
            try
            {

                File.WriteAllBytes(savePath, httpResponse.Content.ReadAsByteArrayAsync().Result);

                var upRemote = false;

                if (upRemote)
                {
                    //将文件转存至 oss 并清理本地文件
                    var oss = new Common.AliYun.OssHelper();
                    var upload = oss.FileUpload(savePath, "uploads/" + DateTime.UtcNow.ToString("yyyy/MM/dd"), Path.GetFileName(SourceUrl!));

                    if (upload)
                    {
                        Common.IOHelper.DeleteFile(savePath);

                        ServerUrl = "uploads/" + DateTime.UtcNow.ToString("yyyy/MM/dd") + "/" + Path.GetFileName(savePath);
                        State = "SUCCESS";
                    }
                    else
                    {
                        State = "抓取错误：阿里云OSS转存失败";
                    }
                }
                else
                {
                    State = "SUCCESS";
                }

            }
            catch (Exception e)
            {
                State = "抓取错误：" + e.Message;
            }
            return this;

        }

        private static bool IsExternalIPAddress(string url)
        {
            var uri = new Uri(url);
            switch (uri.HostNameType)
            {
                case UriHostNameType.Dns:
                    var ipHostEntry = Dns.GetHostEntry(uri.DnsSafeHost);
                    foreach (IPAddress ipAddress in ipHostEntry.AddressList)
                    {
                        _ = ipAddress.GetAddressBytes();
                        if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            if (!IsPrivateIP(ipAddress))
                            {
                                return true;
                            }
                        }
                    }
                    break;

                case UriHostNameType.IPv4:
                    return !IsPrivateIP(IPAddress.Parse(uri.DnsSafeHost));
            }
            return false;
        }

        private static bool IsPrivateIP(IPAddress myIPAddress)
        {
            if (IPAddress.IsLoopback(myIPAddress)) return true;
            if (myIPAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                byte[] ipBytes = myIPAddress.GetAddressBytes();
                // 10.0.0.0/24 
                if (ipBytes[0] == 10)
                {
                    return true;
                }
                // 172.16.0.0/16
                else if (ipBytes[0] == 172 && ipBytes[1] == 16)
                {
                    return true;
                }
                // 192.168.0.0/16
                else if (ipBytes[0] == 192 && ipBytes[1] == 168)
                {
                    return true;
                }
                // 169.254.0.0/16
                else if (ipBytes[0] == 169 && ipBytes[1] == 254)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
