using COSXML;
using COSXML.Auth;
using COSXML.Model.Object;
using COSXML.Model.Tag;
using COSXML.Transfer;
using FileStorage.TencentCloud.Models;
using Microsoft.Extensions.Options;
using System.Text;
using System.Web;

namespace FileStorage.TencentCloud
{

    /// <summary>
    /// 腾讯云COS文件存储
    /// </summary>
    public class TencentCloudStorage : IFileStorage
    {

        private readonly string appId;
        private readonly string region;
        private readonly string bucketName;
        private readonly string url;



        private readonly CosXmlServer cosXml;



        public TencentCloudStorage(IOptionsMonitor<FileStorageSetting> config)
        {
            appId = config.CurrentValue.AppId;
            region = config.CurrentValue.Region;
            bucketName = config.CurrentValue.BucketName;
            url = config.CurrentValue.Url;

            CosXmlConfig cosXmlConfig = new CosXmlConfig.Builder()
                        .SetConnectionTimeoutMs(60000)  //设置连接超时时间，单位毫秒，默认45000ms
                        .SetReadWriteTimeoutMs(40000)  //设置读写超时时间，单位毫秒，默认45000ms
                        .IsHttps(true)  //设置默认 HTTPS 请求
                        .SetAppid(appId) //设置腾讯云账户的账户标识 APPID
                        .SetRegion(region) //设置一个默认的存储桶地域
                    .Build();


            long durationSecond = 600;          //每次请求签名有效时长，单位为秒

            QCloudCredentialProvider qCloudCredentialProvider = new DefaultQCloudCredentialProvider(config.CurrentValue.SecretId, config.CurrentValue.SecretKey, durationSecond);

            cosXml = new CosXmlServer(cosXmlConfig, qCloudCredentialProvider);
        }




        public async Task<bool> FileDeleteAsync(string remotePath)
        {
            try
            {
                remotePath = remotePath.Replace("\\", "/");

                DeleteObjectRequest request = new(bucketName, remotePath);

                var result = await Task.Run(() => cosXml.DeleteObject(request));

                return true;
            }
            catch
            {
                return false;
            }
        }



        public async Task<bool> FileDownloadAsync(string remotePath, string localPath)
        {
            try
            {
                remotePath = remotePath.Replace("\\", "/");

                TransferConfig transferConfig = new();

                TransferManager transferManager = new(cosXml, transferConfig);

                string localDir = localPath[..(localPath.LastIndexOf('/') + 1)];
                Directory.CreateDirectory(localDir);

                string localFileName = localPath[(localPath.LastIndexOf('/') + 1)..];

                // 下载对象
                COSXMLDownloadTask downloadTask = new(bucketName, remotePath, localDir, localFileName);

                await transferManager.DownloadAsync(downloadTask);

                return true;
            }
            catch
            {
                return false;
            }
        }



        public async Task<bool> FileUploadAsync(string localPath, string remotePath, bool isPublicRead, string? fileName = null)
        {
            try
            {
                remotePath = remotePath.Replace("\\", "/");

                TransferConfig transferConfig = new();

                TransferManager transferManager = new(cosXml, transferConfig);

                PutObjectRequest request = new(bucketName, remotePath, localPath);

                if (fileName != null)
                {
                    request.SetRequestHeader("Content-Disposition", string.Format("attachment;filename*=utf-8''{0}", HttpUtility.UrlEncode(fileName, Encoding.UTF8)));
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

                await transferManager.UploadAsync(uploadTask);

                return true;
            }
            catch
            {
                return false;
            }
        }



        public async Task<string?> GetFileUrlAsync(string remotePath, TimeSpan expiry, bool isInline = false)
        {
            try
            {
                remotePath = remotePath.Replace("\\", "/");

                PreSignatureStruct preSignatureStruct = new()
                {
                    appid = appId,//腾讯云账号 APPID
                    region = region, //存储桶地域
                    bucket = bucketName, //存储桶
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

                string requestSignUrl = await Task.Run(() => cosXml.GenerateSignURL(preSignatureStruct));
                return requestSignUrl;
            }
            catch
            {
                return null;
            }
        }
    }
}
