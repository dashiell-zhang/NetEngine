using Application.Model.Basic.File;
using Application.Service.Basic;
using Common;
using System.Net;
using System.Net.Sockets;

namespace Admin.WebAPI.Libraries.Ueditor;

/// <summary>
/// 处理 UEditor 远程图片抓取
/// </summary>
public class CrawlerHandler(string rootPath, HttpContext httpContext, FileService fileService, IHttpClientFactory httpClientFactory, CrawlerConfig crawlerConfig, long uploadKey, string fileServerUrl)
{

    /// <summary>
    /// 处理远程图片抓取请求
    /// </summary>
    /// <returns>UEditor 抓取响应</returns>
    public async Task<string> ProcessAsync()
    {

        var form = await httpContext.Request.ReadFormAsync();
        string[] sources = form["source[]"]!;

        if (sources.Length == 0)
        {
            return JsonHelper.ObjectToJson(new
            {
                state = "参数错误：没有指定抓取源"
            });
        }

        List<CrawlerResult> results = [];

        foreach (var source in sources)
        {
            results.Add(await FetchAsync(source));
        }

        return JsonHelper.ObjectToJson(new
        {
            state = "SUCCESS",
            list = results.Select(t => new
            {
                state = t.State,
                source = t.SourceUrl,
                url = t.ServerUrl,
                fileId = t.FileId?.ToString()
            })
        });

    }


    /// <summary>
    /// 抓取并保存单个远程图片
    /// </summary>
    /// <param name="sourceUrl">远程图片地址</param>
    /// <returns>单个远程图片抓取结果</returns>
    private async Task<CrawlerResult> FetchAsync(string sourceUrl)
    {

        CrawlerResult result = new()
        {
            SourceUrl = sourceUrl
        };

        string? tempFilePath = null;

        try
        {
            if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var sourceUri) || !await IsExternalAddressAsync(sourceUri))
            {
                result.State = "INVALID_Url";
                return result;
            }

            var originalFileName = Path.GetFileName(sourceUri.LocalPath);
            var extension = Path.GetExtension(originalFileName).ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(originalFileName) || !crawlerConfig.AllowExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                result.State = "不允许的文件格式";
                return result;
            }

            var httpClient = httpClientFactory.CreateClient();
            using HttpRequestMessage request = new(HttpMethod.Get, sourceUri)
            {
                Version = HttpVersion.Version20,
                VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
            };
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                result.State = "Url returns " + response.StatusCode;
                return result;
            }

            if (response.Content.Headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true)
            {
                result.State = "Url is not an image";
                return result;
            }

            if (response.Content.Headers.ContentLength > crawlerConfig.SizeLimit)
            {
                result.State = "文件大小超出服务器限制";
                return result;
            }

            var fileBytes = await response.Content.ReadAsByteArrayAsync();

            if (fileBytes.LongLength > crawlerConfig.SizeLimit)
            {
                result.State = "文件大小超出服务器限制";
                return result;
            }

            var tempDirectory = Path.Combine(rootPath, "temps");
            Directory.CreateDirectory(tempDirectory);
            tempFilePath = Path.Combine(tempDirectory, Guid.NewGuid() + extension);
            await File.WriteAllBytesAsync(tempFilePath, fileBytes);

            UploadFileDto uploadFile = new()
            {
                Business = "Article",
                Key = uploadKey,
                Sign = "content-remote-image",
                IsPublicRead = true,
                FileName = originalFileName,
                TempFilePath = tempFilePath
            };

            var fileId = await fileService.UploadFileAsync(rootPath, uploadFile);
            var fileUrl = await fileService.GetFileUrlAsync(fileId);

            result.State = "SUCCESS";
            result.FileId = fileId;
            result.ServerUrl = NormalizeResultUrl(fileUrl ?? string.Empty);
        }
        catch (Exception ex)
        {
            result.State = "抓取错误：" + ex.Message;
        }
        finally
        {
            if (tempFilePath != null)
            {
                IOHelper.DeleteFile(tempFilePath);
            }
        }

        return result;

    }


    /// <summary>
    /// 判断远程地址是否为外部网络地址
    /// </summary>
    /// <param name="uri">远程地址</param>
    /// <returns>是否允许抓取</returns>
    private static async Task<bool> IsExternalAddressAsync(Uri uri)
    {

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (uri.HostNameType == UriHostNameType.IPv4)
        {
            return !StringHelper.IsLanIpAddressV4(IPAddress.Parse(uri.DnsSafeHost).ToString());
        }

        if (uri.HostNameType != UriHostNameType.Dns)
        {
            return false;
        }

        var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost);
        var ipv4Addresses = addresses.Where(t => t.AddressFamily == AddressFamily.InterNetwork).ToList();

        return ipv4Addresses.Count > 0 && ipv4Addresses.All(t => !StringHelper.IsLanIpAddressV4(t.ToString()));

    }


    /// <summary>
    /// 将统一文件地址转换为 UEditor 前缀可拼接的相对地址
    /// </summary>
    /// <param name="fileUrl">统一文件访问地址</param>
    /// <returns>UEditor 响应地址</returns>
    private string NormalizeResultUrl(string fileUrl)
    {

        if (string.IsNullOrWhiteSpace(fileServerUrl) || !fileUrl.StartsWith(fileServerUrl, StringComparison.OrdinalIgnoreCase))
        {
            return fileUrl;
        }

        var relativeUrl = fileUrl[fileServerUrl.Length..].TrimStart('/');
        return fileServerUrl.EndsWith('/') ? relativeUrl : "/" + relativeUrl;

    }

}

/// <summary>
/// 表示 UEditor 远程图片抓取配置
/// </summary>
public class CrawlerConfig
{

    /// <summary>
    /// 获取或设置允许的图片扩展名
    /// </summary>
    public string[] AllowExtensions { get; set; } = [];


    /// <summary>
    /// 获取或设置远程图片大小限制
    /// </summary>
    public long SizeLimit { get; set; }

}

/// <summary>
/// 表示单个远程图片抓取结果
/// </summary>
public class CrawlerResult
{

    /// <summary>
    /// 获取或设置远程图片原始地址
    /// </summary>
    public string SourceUrl { get; set; } = string.Empty;


    /// <summary>
    /// 获取或设置保存后的访问地址
    /// </summary>
    public string? ServerUrl { get; set; }


    /// <summary>
    /// 获取或设置抓取状态
    /// </summary>
    public string? State { get; set; }


    /// <summary>
    /// 获取或设置统一文件标识
    /// </summary>
    public long? FileId { get; set; }

}
