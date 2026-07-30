using System.Text;

namespace Common;

/// <summary>
/// 扩展 HttpClient 集成常用方法
/// </summary>
public static class HttpClientExtension
{


#if !BROWSER
    /// <summary>
    /// 下载远程文件保存到本地（异步）
    /// </summary>
    /// <param name="httpClient">HTTP 客户端</param>
    /// <param name="url">请求地址</param>
    /// <param name="folderPath">保存目录</param>
    /// <param name="fileName">保存文件名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>下载文件的完整路径</returns>
    public static async Task<string> DownloadFileAsync(this HttpClient httpClient, string url, string folderPath, string? fileName = null, CancellationToken cancellationToken = default)
    {
        string? tempFilePath = null;

        try
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            using var httpResponse = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();

            fileName = GetDownloadFileName(url, httpResponse, fileName);

            string filePath = Path.Combine(folderPath, fileName);
            tempFilePath = filePath + "." + Guid.NewGuid().ToString("N") + ".download";

            await using (var contentStream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken))
            await using (var fileStream = File.Create(tempFilePath))
            {
                await contentStream.CopyToAsync(fileStream, cancellationToken);
                await fileStream.FlushAsync(cancellationToken);
            }

            File.Move(tempFilePath, filePath, true);
            tempFilePath = null;

            return filePath;
        }
        catch
        {
            if (tempFilePath != null)
            {
                IOHelper.DeleteFile(tempFilePath);
            }

            throw;
        }
    }


    /// <summary>
    /// 获取下载文件名
    /// </summary>
    private static string GetDownloadFileName(string url, HttpResponseMessage httpResponse, string? fileName)
    {
        fileName = string.IsNullOrWhiteSpace(fileName) ? httpResponse.Content.Headers.ContentDisposition?.FileNameStar : fileName;
        fileName = string.IsNullOrWhiteSpace(fileName) ? httpResponse.Content.Headers.ContentDisposition?.FileName : fileName;

        if (string.IsNullOrWhiteSpace(fileName) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            fileName = System.Web.HttpUtility.UrlDecode(Path.GetFileName(uri.LocalPath));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = Guid.NewGuid().ToString("N");
        }

        fileName = fileName.Trim('"');
        fileName = Path.GetFileName(fileName);

        return string.IsNullOrWhiteSpace(fileName) ? Guid.NewGuid().ToString("N") : fileName;
    }
#endif



    /// <summary>
    /// Get方式获取远程资源
    /// </summary>
    /// <param name="httpClient">HTTP 客户端</param>
    /// <param name="url">请求地址</param>
    /// <param name="headers">自定义Header集合</param>
    /// <param name="options">自定义请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<HttpResponseMessage> GetAsync(this HttpClient httpClient, string url, Dictionary<string, string>? headers = default, Dictionary<string, object>? options = default, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new()
        {
            RequestUri = new Uri(url),
            Method = HttpMethod.Get,
            Version = httpClient.DefaultRequestVersion,
            VersionPolicy = httpClient.DefaultVersionPolicy
        };

        request.SetHeadersAndOptions(headers, options);

        return await httpClient.SendAsync(request, cancellationToken);
    }



    /// <summary>
    /// Post json或xml 数据到指定url
    /// </summary>
    /// <param name="httpClient">HTTP 客户端</param>
    /// <param name="url">Url</param>
    /// <param name="data">数据</param>
    /// <param name="type">json,xml</param>
    /// <param name="headers">自定义Header集合</param>
    /// <param name="options">自定义请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<HttpResponseMessage> PostAsync(this HttpClient httpClient, string url, string data, string type, Dictionary<string, string>? headers = default, Dictionary<string, object>? options = default, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new()
        {
            RequestUri = new Uri(url),
            Method = HttpMethod.Post,
            Version = httpClient.DefaultRequestVersion,
            VersionPolicy = httpClient.DefaultVersionPolicy
        };

        string mediaType;

        if (string.Equals(type, "json", StringComparison.OrdinalIgnoreCase))
        {
            mediaType = "application/json";
        }
        else if (string.Equals(type, "xml", StringComparison.OrdinalIgnoreCase))
        {
            mediaType = "text/xml";
        }
        else
        {
            throw new ArgumentException("type 无效，只能是 json 或 xml", nameof(type));
        }

        request.Content = new StringContent(data, Encoding.UTF8, mediaType);

        request.SetHeadersAndOptions(headers, options);

        return await httpClient.SendAsync(request, cancellationToken);
    }



    /// <summary>
    /// Delete 方式发出请求
    /// </summary>
    /// <param name="httpClient">HTTP 客户端</param>
    /// <param name="url">Url</param>
    /// <param name="headers">自定义Header集合</param>
    /// <param name="options">自定义请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<HttpResponseMessage> DeleteAsync(this HttpClient httpClient, string url, Dictionary<string, string>? headers = default, Dictionary<string, object>? options = default, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new()
        {
            RequestUri = new Uri(url),
            Method = HttpMethod.Delete,
            Version = httpClient.DefaultRequestVersion,
            VersionPolicy = httpClient.DefaultVersionPolicy
        };

        request.SetHeadersAndOptions(headers, options);

        return await httpClient.SendAsync(request, cancellationToken);
    }




    /// <summary>
    /// Post表单数据到指定url
    /// </summary>
    /// <param name="httpClient">HTTP 客户端</param>
    /// <param name="url"></param>
    /// <param name="formItems">Post表单内容</param>
    /// <param name="headers">自定义Header集合</param>
    /// <param name="options">自定义请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<HttpResponseMessage> PostFormAsync(this HttpClient httpClient, string url, Dictionary<string, string> formItems, Dictionary<string, string>? headers = default, Dictionary<string, object>? options = default, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new()
        {
            RequestUri = new Uri(url),
            Method = HttpMethod.Post,
            Version = httpClient.DefaultRequestVersion,
            VersionPolicy = httpClient.DefaultVersionPolicy
        };

        FormUrlEncodedContent content = new(formItems);
        content.Headers.ContentType!.CharSet = "utf-8";

        request.Content = content;

        request.SetHeadersAndOptions(headers, options);

        return await httpClient.SendAsync(request, cancellationToken);
    }




    /// <summary>
    /// Post文件和数据到指定url
    /// </summary>
    /// <param name="httpClient">HTTP 客户端</param>
    /// <param name="url"></param>
    /// <param name="formItems">Post表单内容</param>
    /// <param name="headers">自定义Header集合</param>
    /// <param name="options">自定义请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<HttpResponseMessage> PostFormDataAsync(this HttpClient httpClient, string url, List<PostFormDataItem> formItems, Dictionary<string, string>? headers = default, Dictionary<string, object>? options = default, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new()
        {
            RequestUri = new Uri(url),
            Method = HttpMethod.Post,
            Version = httpClient.DefaultRequestVersion,
            VersionPolicy = httpClient.DefaultVersionPolicy
        };

        foreach (var item in formItems)
        {
            ValidatePostFormDataItem(item);
        }

        string boundary = "----" + Guid.NewGuid().ToString("N");

        MultipartFormDataContent content = new(boundary);
        request.Content = content;

        foreach (var item in formItems)
        {
            if (item.IsFile)
            {
                //上传文件
                content.Add(new StreamContent(item.FileContent!), item.Key!, item.FileName!);
            }
            else
            {
                //上传文本
                content.Add(new StringContent(item.Value!), item.Key!);
            }
        }

        request.SetHeadersAndOptions(headers, options);

        return await httpClient.SendAsync(request, cancellationToken);
    }


    /// <summary>
    /// 验证 FormData 表单项
    /// </summary>
    private static void ValidatePostFormDataItem(PostFormDataItem item)
    {
        if (item == null)
        {
            throw new ArgumentException("表单项不能为空", nameof(item));
        }

        if (string.IsNullOrWhiteSpace(item.Key))
        {
            throw new ArgumentException("表单项 Key 不能为空", nameof(item));
        }

        if (!item.IsFile && item.Value == null)
        {
            throw new ArgumentException("文本表单项 Value 不能为空", nameof(item));
        }
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
                if (FileContent == null)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(FileName))
                {
                    throw new Exception("上传文件时 FileName 属性值不能为空");
                }

                return true;
            }
        }



        /// <summary>
        /// 上传的文件名
        /// </summary>
        public string? FileName { set; get; }



        /// <summary>
        /// 上传的文件内容，发送完成后由本方法释放
        /// </summary>
        public Stream? FileContent { set; get; }


    }



    /// <summary>
    /// 为请求设置 Headers 和 Options
    /// </summary>
    private static HttpRequestMessage SetHeadersAndOptions(this HttpRequestMessage request, Dictionary<string, string>? headers = default, Dictionary<string, object>? options = default)
    {
        if (headers != default)
        {
            foreach (var header in headers)
            {
                if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
                {
                    request.Content?.Headers.Remove(header.Key);
                    request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }

        if (options != default)
        {
            foreach (var option in options)
            {
                request.Options.TryAdd(option.Key, option.Value);
            }
        }

        return request;
    }

}
