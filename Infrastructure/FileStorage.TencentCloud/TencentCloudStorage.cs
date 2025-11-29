using COSXML;
using COSXML.Auth;
using COSXML.Model.Object;
using COSXML.Model.Tag;
using COSXML.Transfer;
using FileStorage.TencentCloud.Models;
using Microsoft.Extensions.Options;
using System.Text;
using System.Web;

namespace FileStorage.TencentCloud;

/// <summary>
/// 腾讯云COS文件存储
/// </summary>
public class TencentCloudStorage : IFileStorage
{

    private readonly FileStorageSetting storageSetting;

    private readonly CosXmlServer cosXml;


    public TencentCloudStorage(IOptionsMonitor<FileStorageSetting> config)
    {

        storageSetting = config.CurrentValue;

        CosXmlConfig cosXmlConfig = new CosXmlConfig.Builder()
                    .SetAppid(storageSetting.AppId)
                    .SetRegion(storageSetting.Region)
                .Build();

        long durationSecond = 600;          //每次请求签名有效时长，单位为秒

        QCloudCredentialProvider qCloudCredentialProvider = new DefaultQCloudCredentialProvider(config.CurrentValue.SecretId, config.CurrentValue.SecretKey, durationSecond);

        cosXml = new(cosXmlConfig, qCloudCredentialProvider);
    }



    public async Task<bool> FileDeleteAsync(string remotePath)
    {
        DeleteObjectRequest request = new(storageSetting.BucketName, remotePath);

        var result = await cosXml.ExecuteAsync<DeleteObjectResult>(request);

        return result.IsSuccessful();
    }


    public async Task<bool> FileDownloadAsync(string remotePath, string localPath)
    {

        TransferConfig transferConfig = new();

        TransferManager transferManager = new(cosXml, transferConfig);

        string localDir = localPath[..(localPath.LastIndexOf('/') + 1)];
        Directory.CreateDirectory(localDir);

        string localFileName = localPath[(localPath.LastIndexOf('/') + 1)..];

        COSXMLDownloadTask downloadTask = new(storageSetting.BucketName, remotePath, localDir, localFileName);

        var result = await transferManager.DownloadAsync(downloadTask);

        return result.IsSuccessful();

    }


    public async Task<bool> FileUploadAsync(string localPath, string remotePath, bool isPublicRead, string? fileName = null)
    {

        remotePath = remotePath.Replace("\\", "/");

        TransferConfig transferConfig = new();

        TransferManager transferManager = new(cosXml, transferConfig);

        PutObjectRequest request = new(storageSetting.BucketName, remotePath, localPath);

        if (fileName != null)
        {
            request.SetRequestHeader("Content-Disposition", string.Format("attachment;filename*=UTF-8''{0}", Uri.EscapeDataString(fileName)));
        }

        if (isPublicRead)
        {
            request.SetCosACL(COSXML.Common.CosACL.PublicRead);
        }
        else
        {
            request.SetCosACL(COSXML.Common.CosACL.Private);
        }


        COSXMLUploadTask uploadTask = new(request);

        uploadTask.SetSrcPath(localPath);

        var result = await transferManager.UploadAsync(uploadTask);

        return result.IsSuccessful();
    }


    public string? GetFileUrl(string remotePath, TimeSpan expiry, bool isInline = false)
    {
        remotePath = remotePath.Replace("\\", "/");

        PreSignatureStruct preSignatureStruct = new()
        {
            appid = storageSetting.AppId,//腾讯云账号 APPID
            region = storageSetting.Region, //存储桶地域
            bucket = storageSetting.BucketName, //存储桶
            key = remotePath, //对象键
            httpMethod = "GET", //HTTP 请求方法
            isHttps = true, //生成 HTTPS 请求 Url
            signDurationSecond = Convert.ToInt64(expiry.TotalSeconds), //请求签名时间,单位秒
            headers = null//签名中需要校验的 header
        };

        if (isInline)
        {
            preSignatureStruct.queryParameters = new()
                {
                    { "response-content-disposition", "inline" }
                };
        }
        else
        {
            preSignatureStruct.queryParameters = null;
        }

        var url = cosXml.GenerateSignURL(preSignatureStruct);

        if (url != null)
        {
            Uri tempUrl = new(url.ToString());

            return storageSetting.Url + tempUrl.PathAndQuery[1..];
        }

        return null;
    }
}
