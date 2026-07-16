using Application.Model.Basic.File;
using Application.Service.Basic;
using Common;

namespace Admin.WebAPI.Libraries.Ueditor;

/// <summary>
/// 处理 UEditor 普通文件和 Base64 文件上传
/// </summary>
public class UploadHandler(UploadConfig uploadConfig, string rootPath, HttpContext httpContext, FileService fileService, long uploadKey, string sign, string fileServerUrl)
{

    /// <summary>
    /// 处理 UEditor 文件上传请求
    /// </summary>
    /// <returns>UEditor 上传响应</returns>
    public async Task<string> ProcessAsync()
    {

        UploadResult result = new();

        try
        {
            var form = await httpContext.Request.ReadFormAsync();

            if (uploadConfig.Base64)
            {
                var base64Value = form[uploadConfig.UploadFieldName].ToString();

                if (string.IsNullOrWhiteSpace(base64Value))
                {
                    result.State = UploadState.InvalidRequest;
                    result.ErrorMessage = "上传内容为空";
                    return WriteResult(result);
                }

                byte[] fileBytes;

                try
                {
                    fileBytes = Convert.FromBase64String(base64Value);
                }
                catch (FormatException)
                {
                    result.State = UploadState.InvalidRequest;
                    result.ErrorMessage = "Base64 文件内容格式不正确";
                    return WriteResult(result);
                }

                if (!CheckFileSize(fileBytes.LongLength))
                {
                    result.State = UploadState.SizeLimitExceed;
                    return WriteResult(result);
                }

                return await SaveFileAsync(uploadConfig.Base64Filename, async tempFilePath => await File.WriteAllBytesAsync(tempFilePath, fileBytes));
            }

            var file = form.Files.GetFile(uploadConfig.UploadFieldName);

            if (file == null || file.Length == 0)
            {
                result.State = UploadState.InvalidRequest;
                result.ErrorMessage = "上传文件为空";
                return WriteResult(result);
            }

            if (!CheckFileType(file.FileName))
            {
                result.State = UploadState.TypeNotAllow;
                return WriteResult(result);
            }

            if (!CheckFileSize(file.Length))
            {
                result.State = UploadState.SizeLimitExceed;
                return WriteResult(result);
            }

            return await SaveFileAsync(file.FileName, async tempFilePath =>
            {

                await using FileStream fileStream = new(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
                await file.CopyToAsync(fileStream);

            });
        }
        catch (Exception ex)
        {
            result.State = UploadState.FileAccessError;
            result.ErrorMessage = ex.Message;
            return WriteResult(result);
        }

    }


    /// <summary>
    /// 将临时文件交给统一文件服务保存
    /// </summary>
    /// <param name="originalFileName">原始文件名</param>
    /// <param name="writeTempFileAsync">临时文件写入方法</param>
    /// <returns>UEditor 上传响应</returns>
    private async Task<string> SaveFileAsync(string originalFileName, Func<string, Task> writeTempFileAsync)
    {

        UploadResult result = new()
        {
            OriginFileName = originalFileName
        };

        if (!CheckFileType(originalFileName))
        {
            result.State = UploadState.TypeNotAllow;
            return WriteResult(result);
        }

        var tempDirectory = Path.Combine(rootPath, "temps");
        Directory.CreateDirectory(tempDirectory);

        var tempFilePath = Path.Combine(tempDirectory, Guid.NewGuid() + Path.GetExtension(originalFileName).ToLowerInvariant());

        try
        {
            await writeTempFileAsync(tempFilePath);

            UploadFileDto uploadFile = new()
            {
                Business = "Article",
                Key = uploadKey,
                Sign = sign,
                IsPublicRead = true,
                FileName = originalFileName,
                TempFilePath = tempFilePath
            };

            var fileId = await fileService.UploadFileAsync(rootPath, uploadFile);
            var fileUrl = await fileService.GetFileUrlAsync(fileId);

            result.State = UploadState.Success;
            result.FileId = fileId;
            result.Url = NormalizeResultUrl(fileUrl ?? string.Empty);
        }
        catch (Exception ex)
        {
            result.State = UploadState.FileAccessError;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            IOHelper.DeleteFile(tempFilePath);
        }

        return WriteResult(result);

    }


    /// <summary>
    /// 检查文件扩展名是否允许
    /// </summary>
    /// <param name="fileName">文件名</param>
    /// <returns>是否允许上传</returns>
    private bool CheckFileType(string fileName)
    {

        var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
        return uploadConfig.AllowExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase);

    }


    /// <summary>
    /// 检查文件大小是否超过配置限制
    /// </summary>
    /// <param name="size">文件大小</param>
    /// <returns>是否允许上传</returns>
    private bool CheckFileSize(long size)
    {

        return size <= uploadConfig.SizeLimit;

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


    /// <summary>
    /// 生成 UEditor 上传响应
    /// </summary>
    /// <param name="result">上传结果</param>
    /// <returns>UEditor JSON 响应</returns>
    private static string WriteResult(UploadResult result)
    {

        return JsonHelper.ObjectToJson(new
        {
            state = result.State == UploadState.Success ? "SUCCESS" : GetStateMessage(result.State),
            url = result.Url,
            title = result.OriginFileName,
            original = result.OriginFileName,
            fileId = result.FileId?.ToString(),
            error = result.ErrorMessage
        });

    }


    /// <summary>
    /// 获取 UEditor 上传状态说明
    /// </summary>
    /// <param name="state">上传状态</param>
    /// <returns>状态说明</returns>
    private static string GetStateMessage(UploadState state)
    {

        return state switch
        {
            UploadState.FileAccessError => "文件访问出错，请检查写入权限",
            UploadState.SizeLimitExceed => "文件大小超出服务器限制",
            UploadState.TypeNotAllow => "不允许的文件格式",
            UploadState.NetworkError => "网络错误",
            UploadState.InvalidRequest => "上传请求不正确",
            _ => "未知错误"
        };

    }

}

/// <summary>
/// 表示 UEditor 上传配置
/// </summary>
public class UploadConfig
{

    /// <summary>
    /// 获取或设置上传表单域名称
    /// </summary>
    public string UploadFieldName { get; set; } = string.Empty;


    /// <summary>
    /// 获取或设置上传大小限制
    /// </summary>
    public long SizeLimit { get; set; }


    /// <summary>
    /// 获取或设置允许的文件扩展名
    /// </summary>
    public string[] AllowExtensions { get; set; } = [];


    /// <summary>
    /// 获取或设置文件是否以 Base64 形式上传
    /// </summary>
    public bool Base64 { get; set; }


    /// <summary>
    /// 获取或设置 Base64 文件名
    /// </summary>
    public string Base64Filename { get; set; } = string.Empty;

}

/// <summary>
/// 表示 UEditor 上传结果
/// </summary>
public class UploadResult
{

    /// <summary>
    /// 获取或设置上传状态
    /// </summary>
    public UploadState State { get; set; } = UploadState.Unknown;


    /// <summary>
    /// 获取或设置文件访问地址
    /// </summary>
    public string? Url { get; set; }


    /// <summary>
    /// 获取或设置原始文件名
    /// </summary>
    public string? OriginFileName { get; set; }


    /// <summary>
    /// 获取或设置统一文件标识
    /// </summary>
    public long? FileId { get; set; }


    /// <summary>
    /// 获取或设置错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

}

/// <summary>
/// 定义 UEditor 上传状态
/// </summary>
public enum UploadState
{

    Success = 0,
    SizeLimitExceed = -1,
    TypeNotAllow = -2,
    FileAccessError = -3,
    NetworkError = -4,
    InvalidRequest = -5,
    Unknown = 1

}
