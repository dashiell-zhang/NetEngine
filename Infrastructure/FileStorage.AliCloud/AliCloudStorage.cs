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
    public class AliCloudStorage(IOptionsMonitor<FileStorageSetting> config, IHttpClientFactory httpClientFactory) : IFileStorage
    {

        private readonly string endpoint = config.CurrentValue.Endpoint;
        private readonly string accessKeyId = config.CurrentValue.AccessKeyId;
        private readonly string accessKeySecret = config.CurrentValue.AccessKeySecret;
        private readonly string bucketName = config.CurrentValue.BucketName;


        public async Task<bool> FileDeleteAsync(string remotePath)
        {
            var httpClient = httpClientFactory.CreateClient();

            Configuration cfg = new()
            {
                Region = "cn-shanghai",
                Endpoint = endpoint,
                CredentialsProvider = new AlibabaCloud.OSS.V2.Credentials.StaticCredentialsProvide(accessKeyId, accessKeySecret)
            };
            cfg.HttpTransport = new(httpClient);

            using Client client = new(cfg);

            var result = await client.DeleteObjectAsync(new()
            {
                Bucket = bucketName,
                Key = remotePath
            });

            return result.StatusCode == 200;
        }


        public async Task<bool> FileDownloadAsync(string remotePath, string localPath)
        {
            var httpClient = httpClientFactory.CreateClient();

            Configuration cfg = new()
            {
                Region = "cn-shanghai",
                Endpoint = endpoint,
                CredentialsProvider = new AlibabaCloud.OSS.V2.Credentials.StaticCredentialsProvide(accessKeyId, accessKeySecret)
            };
            cfg.HttpTransport = new(httpClient);

            using Client client = new(cfg);

            var result = await client.GetObjectToFileAsync(new()
            {
                Bucket = bucketName,
                Key = remotePath,
            }, localPath);


            return result.StatusCode == 200;
        }


        public async Task<bool> FileUploadAsync(string localPath, string remotePath, bool isPublicRead, string? fileName = null)
        {

            try
            {
                var objectName = Path.GetFileName(localPath);

                objectName = Path.Combine(remotePath, objectName).Replace("\\", "/");

                var httpClient = httpClientFactory.CreateClient();

                Configuration cfg = new()
                {
                    Region = "cn-shanghai",
                    Endpoint = endpoint,
                    CredentialsProvider = new AlibabaCloud.OSS.V2.Credentials.StaticCredentialsProvide(accessKeyId, accessKeySecret)
                };
                cfg.HttpTransport = new(httpClient);

                using Client client = new(cfg);

                var result = await client.PutObjectFromFileAsync(new()
                {
                    Bucket = bucketName,
                    Key = objectName,
                    Acl = isPublicRead ? "public-read" : "private",
                    ContentDisposition = fileName != null ? string.Format("attachment;filename*=utf-8''{0}", WebUtility.UrlEncode(fileName)) : null
                }, localPath);

                return true;
            }
            catch
            {
                return false;
            }
        }


        public string? GetFileUrl(string remotePath, TimeSpan expiry, bool isInline = false)
        {
            var httpClient = httpClientFactory.CreateClient();

            Configuration cfg = new()
            {
                Region = "cn-shanghai",
                Endpoint = endpoint,
                CredentialsProvider = new AlibabaCloud.OSS.V2.Credentials.StaticCredentialsProvide(accessKeyId, accessKeySecret)
            };
            cfg.HttpTransport = new(httpClient);

            using Client client = new(cfg);

            var request = client.Presign(new GetObjectRequest()
            {
                Bucket = bucketName,
                Key = remotePath,
                ResponseContentDisposition = isInline ? "inline" : null
            }, DateTime.UtcNow + expiry);

            return request.Url;
        }

    }
}
