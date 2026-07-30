using Application.Model.Basic.File;
using Application.Service.Basic;
using Common;
using System.Net;
using System.Net.Sockets;

namespace Admin.WebAPI.Libraries.Ueditor;

/// <summary>
/// 处理 UEditor 远程图片抓取
/// </summary>
public class CrawlerHandler(string rootPath, HttpContext httpContext, FileService fileService, CrawlerConfig crawlerConfig, long uploadKey, string fileServerUrl)
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
            CancellationToken cancellationToken = httpContext.RequestAborted;

            if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var sourceUri) || !await IsExternalAddressAsync(sourceUri, cancellationToken))
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

            using SocketsHttpHandler handler = new()
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                UseProxy = false,
                ConnectCallback = ConnectPublicAddressAsync
            };
            using HttpClient httpClient = new(handler);
            using HttpRequestMessage request = new(HttpMethod.Get, sourceUri)
            {
                Version = HttpVersion.Version20,
                VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
            };
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

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

            var tempDirectory = Path.Combine(rootPath, "temps");
            Directory.CreateDirectory(tempDirectory);
            string candidateTempFilePath = Path.Combine(tempDirectory, Guid.NewGuid() + extension);

            await using (Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (FileStream tempFileStream = new(candidateTempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                tempFilePath = candidateTempFilePath;
                byte[] buffer = new byte[81920];
                long totalBytes = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    totalBytes += bytesRead;

                    if (totalBytes > crawlerConfig.SizeLimit)
                    {
                        result.State = "文件大小超出服务器限制";
                        return result;
                    }

                    await tempFileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                }
            }

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
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
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
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否允许抓取</returns>
    private static async Task<bool> IsExternalAddressAsync(Uri uri, CancellationToken cancellationToken)
    {

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
        {
            return StringHelper.IsPublicIpAddress(IPAddress.Parse(uri.DnsSafeHost));
        }

        if (uri.HostNameType != UriHostNameType.Dns)
        {
            return false;
        }

        var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);

        return addresses.Length > 0 && addresses.All(StringHelper.IsPublicIpAddress);

    }


    /// <summary>
    /// 连接经过公网地址校验的远程终结点
    /// </summary>
    /// <param name="context">HTTP连接上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已连接的网络流</returns>
    private static async ValueTask<Stream> ConnectPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {

        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);

        if (addresses.Length == 0 || addresses.Any(t => !StringHelper.IsPublicIpAddress(t)))
        {
            throw new HttpRequestException("远程主机解析到了非公网地址");
        }

        Exception? lastException = null;

        foreach (var address in addresses)
        {
            Socket socket = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException)
            {
                socket.Dispose();
                lastException = ex;

                if (ex is OperationCanceledException)
                {
                    throw;
                }
            }
        }

        throw new HttpRequestException("无法连接远程主机", lastException);

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
