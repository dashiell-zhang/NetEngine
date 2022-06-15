using Aliyun.OSS;
using Aliyun.OSS.Util;
using FileStorage.AliCloud.Models;
using Microsoft.Extensions.Options;
using System;
using System.IO;

namespace FileStorage.AliCloud
{


    /// <summary>
    /// 阿里云OSS文件存储
    /// </summary>
    public class AliCloudStorage : IFileStorage
    {

        private readonly string endpoint;
        private readonly string accessKeyId;
        private readonly string accessKeySecret;
        private readonly string bucketName;



        public AliCloudStorage(IOptionsMonitor<StorageSetting> config)
        {
            endpoint = config.CurrentValue.Endpoint;
            accessKeyId = config.CurrentValue.AccessKeyId;
            accessKeySecret = config.CurrentValue.AccessKeySecret;
            bucketName = config.CurrentValue.BucketName;
        }




        public bool FileDelete(string remotePath)
        {
            try
            {
                var client = new OssClient(endpoint, accessKeyId, accessKeySecret);

                client.DeleteObject(bucketName, remotePath);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Delete object failed. {0}", ex.Message);
                return false;
            }
        }



        public bool FileDownload(string remotePath, string localPath)
        {
            var objectName = remotePath;

            var downloadFilename = localPath;
            // 创建OssClient实例。
            var client = new OssClient(endpoint, accessKeyId, accessKeySecret);
            try
            {
                // 下载文件到流。OssObject 包含了文件的各种信息，如文件所在的存储空间、文件名、元信息以及一个输入流。
                var obj = client.GetObject(bucketName, objectName);
                using (var requestStream = obj.Content)
                {
                    byte[] buf = new byte[1024];
                    var fs = File.Open(downloadFilename, FileMode.OpenOrCreate);
                    var len = 0;
                    // 通过输入流将文件的内容读取到文件或者内存中。
                    while ((len = requestStream.Read(buf, 0, 1024)) != 0)
                    {
                        fs.Write(buf, 0, len);
                    }
                    fs.Close();
                }
                Console.WriteLine("Get object succeeded");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Get object failed. {0}", ex.Message);
                return false;
            }
        }



        public bool FileUpload(string localPath, string remotePath, string? fileName = null)
        {
            try
            {
                var objectName = Path.GetFileName(localPath);

                if (remotePath != null)
                {
                    objectName = remotePath + "/" + objectName;
                }

                var localFilename = localPath;


                // 创建OssClient实例。
                var client = new OssClient(endpoint, accessKeyId, accessKeySecret);

                if (fileName != null)
                {
                    var metaData = new ObjectMetadata()
                    {
                        ContentDisposition = string.Format("attachment;filename*=utf-8''{0}", HttpUtils.EncodeUri(fileName, "utf-8"))
                    };

                    // 上传文件。
                    client.PutObject(bucketName, objectName, localFilename, metaData);
                }
                else
                {
                    // 上传文件。
                    client.PutObject(bucketName, objectName, localFilename);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }



        public string? GetFileTempUrl(string remotePath, TimeSpan expiry, string? fileName = null)
        {

            try
            {
                OssClient client = new(endpoint, accessKeyId, accessKeySecret);

                GeneratePresignedUriRequest req = new(bucketName, remotePath);

                if (fileName != null)
                {
                    req.ResponseHeaders.ContentDisposition = string.Format("attachment;filename*=utf-8''{0}", HttpUtils.EncodeUri(fileName, "utf-8"));
                }

                req.Expiration = DateTime.UtcNow + expiry;

                var url = client.GeneratePresignedUri(req);

                return url.ToString();
            }
            catch
            {
                return null;
            }
        }


    }
}
