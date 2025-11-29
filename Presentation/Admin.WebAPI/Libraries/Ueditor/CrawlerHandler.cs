using Common;
using FileStorage;
using System.Net;
using WebAPI.Core.Extensions;

namespace Admin.WebAPI.Libraries.Ueditor;
/// <summary>
/// Crawler 的摘要说明
/// </summary>
public class CrawlerHandler(string rootPath, HttpContext httpContext)
{
    private string[]? Sources;

    public async Task<string> ProcessAsync(string fileServerUrl)
    {
        Sources = httpContext.Current().Request.Form["source[]"];

        if (Sources == null || Sources.Length == 0)
        {
            return JsonHelper.ObjectToJson(new
            {
                state = "参数错误：没有指定抓取源"
            });
        }

        var fileStorage = httpContext.RequestServices.GetService<IFileStorage>();

        List<Crawler> Crawlers = [];

        foreach (var x in Sources)
        {
            Crawler crawler = new(x, rootPath, fileServerUrl);

            await crawler.Fetch(fileStorage);

            Crawlers.Add(crawler);
        }

        return JsonHelper.ObjectToJson(new
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

public class Crawler(string sourceUrl, string rootPath, string fileServerUrl)
{
    public string? SourceUrl { get; set; } = sourceUrl;

    public string? ServerUrl { get; set; }

    public string? State { get; set; }


    public async Task<Crawler> Fetch(IFileStorage? fileStorage)
    {
        if (!IsExternalIPAddress(SourceUrl!))
        {
            State = "INVALID_Url";
            return this;
        }


        using HttpClient client = new();
        client.DefaultRequestVersion = new("2.0");
        using var httpResponse = await client.GetAsync(SourceUrl);
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
        ServerUrl = PathFormatter.Format(Path.GetFileName(SourceUrl!), Config.GetString("catcherPathFormat", fileServerUrl));
        var savePath = Path.Combine(rootPath, ServerUrl);
        if (!Directory.Exists(Path.GetDirectoryName(savePath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        }
        try
        {

            File.WriteAllBytes(savePath, await httpResponse.Content.ReadAsByteArrayAsync());


            if (fileStorage != null)
            {
                var utcNow = DateTime.UtcNow;

                string basePath = Path.Combine("uploads", utcNow.ToString("yyyy"), utcNow.ToString("MM"), utcNow.ToString("dd"));

                var upload = await fileStorage.FileUploadAsync(savePath, basePath, true, Path.GetFileName(SourceUrl!));

                if (upload)
                {
                    IOHelper.DeleteFile(savePath);

                    ServerUrl = Path.Combine(basePath, Path.GetFileName(savePath)).Replace("\\", "/");
                    State = "SUCCESS";
                }
                else
                {
                    State = "抓取错误：文件存储转存失败";
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
        Uri uri = new(url);
        switch (uri.HostNameType)
        {
            case UriHostNameType.Dns:
                var ipHostEntry = Dns.GetHostEntry(uri.DnsSafeHost);
                foreach (IPAddress ipAddress in ipHostEntry.AddressList)
                {
                    _ = ipAddress.GetAddressBytes();
                    if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        if (!StringHelper.IsLanIpAddressV4(ipAddress.ToString()))
                        {
                            return true;
                        }
                    }
                }
                break;

            case UriHostNameType.IPv4:
                return !StringHelper.IsLanIpAddressV4(IPAddress.Parse(uri.DnsSafeHost).ToString());
        }
        return false;
    }


}
