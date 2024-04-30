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
        private readonly string url = config.CurrentValue.URL;

        public bool FileDelete(string remotePath)
        {
            try
            {
                remotePath = remotePath.Replace("\\", "/");

                OssClient client = new(endpoint, accessKeyId, accessKeySecret);

                client.DeleteObject(bucketName, remotePath);

                return true;
            }
            catch
            {
                return false;
            }
        }



        public bool FileDownload(string remotePath, string localPath)
        {
            try
            {
                remotePath = remotePath.Replace("\\", "/");

                OssClient client = new(endpoint, accessKeyId, accessKeySecret);

                // 下载文件到流。OssObject 包含了文件的各种信息，如文件所在的存储空间、文件名、元信息以及一个输入流。
                var obj = client.GetObject(bucketName, remotePath);
                using var requestStream = obj.Content;
                byte[] buf = new byte[1024];
                var fs = File.Open(localPath, FileMode.OpenOrCreate);
                var len = 0;
                // 通过输入流将文件的内容读取到文件或者内存中。
                while ((len = requestStream.Read(buf, 0, 1024)) != 0)
                {
                    fs.Write(buf, 0, len);
                }
                fs.Close();

                return true;
            }
            catch
            {
                return false;
            }
        }



        public bool FileUpload(string localPath, string remotePath, bool isPublicRead, string? fileName = null)
        {
            try
            {
                var objectName = Path.GetFileName(localPath);

                objectName = Path.Combine(remotePath, objectName).Replace("\\", "/");

                // 创建OssClient实例。
                OssClient client = new(endpoint, accessKeyId, accessKeySecret);

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

                return true;
            }
            catch
            {
                return false;
            }
        }



        public string? GetFileTempURL(string remotePath, TimeSpan expiry, string? fileName = null)
        {

            try
            {
                string publicEndpoint = endpoint.Replace("-internal.aliyuncs.com", ".aliyuncs.com");

                remotePath = remotePath.Replace("\\", "/");

                OssClient client = new(publicEndpoint, accessKeyId, accessKeySecret);

                GeneratePresignedUriRequest req = new(bucketName, remotePath);

                if (fileName != null)
                {
                    req.ResponseHeaders.ContentDisposition = string.Format("attachment;filename*=utf-8''{0}", HttpUtils.EncodeUri(fileName, "utf-8"));
                }

                req.Expiration = DateTime.UtcNow + expiry;

                var url = client.GeneratePresignedUri(req);

                Uri tempURL = new(url.ToString());

                return this.url + tempURL.PathAndQuery[1..];


            }
            catch
            {
                return null;
            }
        }


    }
}
