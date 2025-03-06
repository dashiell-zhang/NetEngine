using Aliyun.OSS;
using Aliyun.OSS.Util;
using FileStorage.AliCloud.Models;
using Microsoft.Extensions.Options;

namespace FileStorage.AliCloud
{


    /// <summary>
    /// 阿里云OSS文件存储
    /// </summary>
    public class AliCloudStorage(IOptionsMonitor<FileStorageSetting> config) : IFileStorage
    {

        private readonly string endpoint = config.CurrentValue.Endpoint;
        private readonly string accessKeyId = config.CurrentValue.AccessKeyId;
        private readonly string accessKeySecret = config.CurrentValue.AccessKeySecret;
        private readonly string bucketName = config.CurrentValue.BucketName;
        private readonly string url = config.CurrentValue.Url;

        public async Task<bool> FileDeleteAsync(string remotePath)
        {
            try
            {
                remotePath = remotePath.Replace("\\", "/");

                OssClient client = new(endpoint, accessKeyId, accessKeySecret);

                await Task.Run(() => client.DeleteObject(bucketName, remotePath));

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

                OssClient client = new(endpoint, accessKeyId, accessKeySecret);

                await Task.Run(() =>
                {

                    var obj = client.GetObject(bucketName, remotePath);
                    using var requestStream = obj.Content;
                    byte[] buf = new byte[1024];
                    var fs = File.Open(localPath, FileMode.OpenOrCreate);
                    var len = 0;

                    while ((len = requestStream.Read(buf, 0, 1024)) != 0)
                    {
                        fs.Write(buf, 0, len);
                    }
                    fs.Close();

                });

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
                var objectName = Path.GetFileName(localPath);

                objectName = Path.Combine(remotePath, objectName).Replace("\\", "/");

                OssClient client = new(endpoint, accessKeyId, accessKeySecret);

                await Task.Run(() =>
                {
                    if (fileName != null)
                    {
                        ObjectMetadata metaData = new()
                        {
                            ContentDisposition = string.Format("attachment;filename*=utf-8''{0}", HttpUtils.EncodeUri(fileName, "utf-8"))
                        };

                        client.PutObject(bucketName, objectName, localPath, metaData);
                    }
                    else
                    {
                        client.PutObject(bucketName, objectName, localPath);
                    }

                    if (isPublicRead)
                    {
                        client.SetObjectAcl(bucketName, objectName, CannedAccessControlList.PublicRead);
                    }
                    else
                    {
                        client.SetObjectAcl(bucketName, objectName, CannedAccessControlList.Private);
                    }

                });

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
                string publicEndpoint = endpoint.Replace("-internal.aliyuncs.com", ".aliyuncs.com");

                remotePath = remotePath.Replace("\\", "/");

                OssClient client = new(publicEndpoint, accessKeyId, accessKeySecret);

                GeneratePresignedUriRequest req = new(bucketName, remotePath);

                if (isInline)
                {
                    req.ResponseHeaders.ContentDisposition = "inline";
                }

                req.Expiration = DateTime.UtcNow + expiry;

                var url = await Task.Run(() => client.GeneratePresignedUri(req));

                Uri tempUrl = new(url.ToString());

                return this.url + tempUrl.PathAndQuery[1..];
            }
            catch
            {
                return null;
            }
        }

    }
}
