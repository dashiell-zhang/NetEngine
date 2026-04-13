using Microsoft.AspNetCore.Components.Forms;
using SourceGenerator.Runtime.Attributes;
using SkiaSharp;
using System.Net.Http.Headers;

namespace Admin.App.Services;

/// <summary>
/// 负责处理通用图片压缩与上传
/// </summary>
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class ImageUploadService(IHttpClientFactory httpClientFactory, UploadSignatureService uploadSignatureService)
{

    private const int MaxFileReadSize = int.MaxValue;

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    private readonly UploadSignatureService _uploadSignatureService = uploadSignatureService;


    /// <summary>
    /// 上传图片文件
    /// </summary>
    public async Task<ImageUploadResult> UploadImageAsync(IBrowserFile file, ImageUploadRequest request)
    {

        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Business))
        {
            throw new ArgumentException("Business 不可为空", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Sign))
        {
            throw new ArgumentException("Sign 不可为空", nameof(request));
        }

        var imageBytes = await BuildJpegImageBytesAsync(file, request.MaxSideLength);
        var requestPath = BuildRequestPath(request);
        var signature = _uploadSignatureService.CreateUploadSignature(requestPath, imageBytes);

        using MultipartFormDataContent formDataContent = new("----" + DateTime.UtcNow.Ticks.ToString("x"));
        using HttpRequestMessage requestMessage = new(HttpMethod.Post, requestPath.TrimStart('/'));

        var fileContent = new ByteArrayContent(imageBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        formDataContent.Add(fileContent, "file", BuildTargetFileName(file.Name));

        requestMessage.Content = formDataContent;
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", signature.Authorization);
        requestMessage.Headers.Add("Token", signature.Token);
        requestMessage.Headers.Add("Time", signature.TimeText);

        using var uploadClient = _httpClientFactory.CreateClient("upload");
        using var httpResponse = await uploadClient.SendAsync(requestMessage);

        httpResponse.EnsureSuccessStatusCode();

        var fileId = (await httpResponse.Content.ReadAsStringAsync()).Replace("\"", string.Empty);

        return new ImageUploadResult
        {
            FileId = fileId,
            FileName = file.Name,
            FileBytes = imageBytes,
            ContentType = "image/jpeg"
        };
    }


    /// <summary>
    /// 构建上传请求路径
    /// </summary>
    private static string BuildRequestPath(ImageUploadRequest request)
    {

        return $"/File/UploadFile?business={request.Business}&key={request.Key}&sign={request.Sign}&isPublicRead={request.IsPublicRead.ToString().ToLowerInvariant()}";
    }


    /// <summary>
    /// 生成上传文件名
    /// </summary>
    private static string BuildTargetFileName(string fileName)
    {

        var extension = Path.GetExtension(fileName);

        if (string.IsNullOrWhiteSpace(extension))
        {
            return fileName + ".jpg";
        }

        return fileName[..^extension.Length] + ".jpg";
    }


    /// <summary>
    /// 构建压缩后的 JPEG 字节数组
    /// </summary>
    private static async Task<byte[]> BuildJpegImageBytesAsync(IBrowserFile file, int maxSideLength)
    {

        await using MemoryStream memoryStream = new();
        await file.OpenReadStream(MaxFileReadSize).CopyToAsync(memoryStream);

        using var originalBitmap = SKBitmap.Decode(memoryStream.ToArray());

        if (originalBitmap == null)
        {
            throw new InvalidOperationException("图片内容无法解析");
        }

        var imageInfo = GetResizeImageInfo(originalBitmap.Width, originalBitmap.Height, maxSideLength);
        using var outputBitmap = ResizeBitmap(originalBitmap, imageInfo);
        using var image = SKImage.FromBitmap(outputBitmap);
        using var imageData = image.Encode(SKEncodedImageFormat.Jpeg, 100);

        if (imageData == null)
        {
            throw new InvalidOperationException("图片编码失败");
        }

        using MemoryStream outputStream = new();
        imageData.SaveTo(outputStream);

        return outputStream.ToArray();
    }


    /// <summary>
    /// 计算图片缩放后的尺寸
    /// </summary>
    private static SKImageInfo GetResizeImageInfo(int width, int height, int maxSideLength)
    {

        if (width <= maxSideLength && height <= maxSideLength)
        {
            return new SKImageInfo(width, height);
        }

        if (width > height)
        {
            var ratio = maxSideLength / (float)width;
            return new SKImageInfo((int)(width * ratio), (int)(height * ratio));
        }

        var heightRatio = maxSideLength / (float)height;
        return new SKImageInfo((int)(width * heightRatio), (int)(height * heightRatio));
    }


    /// <summary>
    /// 按目标尺寸缩放图片
    /// </summary>
    private static SKBitmap ResizeBitmap(SKBitmap originalBitmap, SKImageInfo imageInfo)
    {

        if (originalBitmap.Width == imageInfo.Width && originalBitmap.Height == imageInfo.Height)
        {
            return originalBitmap.Copy();
        }

        var resizedBitmap = originalBitmap.Resize(imageInfo, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));

        if (resizedBitmap == null)
        {
            throw new InvalidOperationException("图片缩放失败");
        }

        return resizedBitmap;
    }
}


/// <summary>
/// 表示图片上传请求
/// </summary>
public class ImageUploadRequest
{

    /// <summary>
    /// 业务标识
    /// </summary>
    public string Business { get; set; } = string.Empty;


    /// <summary>
    /// 业务主键
    /// </summary>
    public long Key { get; set; }


    /// <summary>
    /// 上传签名标识
    /// </summary>
    public string Sign { get; set; } = string.Empty;


    /// <summary>
    /// 是否公开读取
    /// </summary>
    public bool IsPublicRead { get; set; } = true;


    /// <summary>
    /// 长边最大尺寸
    /// </summary>
    public int MaxSideLength { get; set; } = 1920;
}


/// <summary>
/// 表示图片上传结果
/// </summary>
public class ImageUploadResult
{

    /// <summary>
    /// 文件标识
    /// </summary>
    public string FileId { get; set; } = string.Empty;


    /// <summary>
    /// 原始文件名
    /// </summary>
    public string FileName { get; set; } = string.Empty;


    /// <summary>
    /// 上传后的文件字节
    /// </summary>
    public byte[] FileBytes { get; set; } = [];


    /// <summary>
    /// 文件内容类型
    /// </summary>
    public string ContentType { get; set; } = string.Empty;
}
