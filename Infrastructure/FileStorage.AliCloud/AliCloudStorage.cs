using AlibabaCloud.OSS.V2;
using AlibabaCloud.OSS.V2.Models;
using FileStorage.AliCloud.Models;
using Microsoft.Extensions.Options;
using System.Net;

namespace FileStorage.AliCloud
{

    /// <summary>
    /// 阿里云OSS文件存储
    /// </summary>
    public class AliCloudStorage : IFileStorage
    {

        private readonly FileStorageSetting storageSetting;

        private readonly Client client;


        public AliCloudStorage(IOptionsMonitor<FileStorageSetting> config, IHttpClientFactory httpClientFactory)
        {
            storageSetting = config.CurrentValue;


            AlibabaCloud.OSS.V2.Credentials.StaticCredentialsProvide credentialsProvide = new(config.CurrentValue.AccessKeyId, config.CurrentValue.AccessKeySecret);

            var httpClient = httpClientFactory.CreateClient();

            Configuration cfg = new()
            {
                Region = storageSetting.Region,
                UseInternalEndpoint = storageSetting.UseInternalEndpoint,
                CredentialsProvider = credentialsProvide
            };

            cfg.HttpTransport = new(httpClient);

            client = new(cfg);
        }



        public async Task<bool> FileDeleteAsync(string remotePath)
        {
            var result = await client.DeleteObjectAsync(new()
            {
                Bucket = storageSetting.BucketName,
                Key = remotePath
            });

            return result.StatusCode == 200;
        }


        public async Task<bool> FileDownloadAsync(string remotePath, string localPath)
        {
            var result = await client.GetObjectToFileAsync(new()
            {
                Bucket = storageSetting.BucketName,
                Key = remotePath,
            }, localPath);


            return result.StatusCode == 200;
        }


        public async Task<bool> FileUploadAsync(string localPath, string remotePath, bool isPublicRead, string? fileName = null)
        {


            var objectName = Path.GetFileName(localPath);

            objectName = Path.Combine(remotePath, objectName).Replace("\\", "/");

            var result = await client.PutObjectFromFileAsync(new()
            {
                Bucket = storageSetting.BucketName,
                Key = objectName,
                Acl = isPublicRead ? "public-read" : "private",
                ContentDisposition = fileName != null ? string.Format("attachment;filename*=utf-8''{0}", WebUtility.UrlEncode(fileName)) : null
            }, localPath);

            return result.StatusCode == 200;

        }


        public string? GetFileUrl(string remotePath, TimeSpan expiry, bool isInline = false)
        {
            var request = client.Presign(new GetObjectRequest()
            {
                Bucket = storageSetting.BucketName,
                Key = remotePath,
                ResponseContentDisposition = isInline ? "inline" : null
            }, DateTime.UtcNow + expiry);

            var url = request.Url;

            if (url != null)
            {
                Uri tempUrl = new(url.ToString());

                return storageSetting.Url + tempUrl.PathAndQuery[1..];
            }

            return null;

        }

    }
}
