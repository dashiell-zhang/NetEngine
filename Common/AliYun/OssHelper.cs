using Aliyun.OSS;
using System;
using System.IO;

namespace Common.AliYun
{
    public class OssHelper
    {
        string endpoint = IO.Config.Get()["OSSEndpoint"];
        string accessKeyId = "";
        string accessKeySecret = "";
        string bucketName = "";

        public OssHelper()
        {

        }


        public OssHelper(string in_endpoint, string in_accessKeyId, string in_accessKeySecret, string in_bucketName)
        {
            endpoint = in_endpoint;
            accessKeyId = in_accessKeyId;
            accessKeySecret = in_accessKeySecret;
            bucketName = in_bucketName;
        }


        /// <summary>
        /// 上传本地文件到 OSS
        /// </summary>
        /// <param name="localpath">本地文件路径</param>
        /// <param name="remotepath">远端文件路径 以 / 分割多级文件夹，不传默认为更目录</param>
        public bool FileUpload(string localpath, string remotepath = null)
        {
            try
            {
                var objectName = System.IO.Path.GetFileName(localpath);

                if (remotepath != null)
                {
                    objectName = remotepath + "/" + objectName;
                }

                var localFilename = localpath;

                // 创建OssClient实例。
                var client = new OssClient(endpoint, accessKeyId, accessKeySecret);

                // 上传文件。
                client.PutObject(bucketName, objectName, localFilename);

                return true;
            }
            catch
            {
                return false;
            }
        }



        /// <summary>
        /// 下载OSS的文件
        /// </summary>
        /// <param name="remotepath"></param>
        /// <param name="localpath"></param>
        /// <returns></returns>
        public bool FileDownload(string remotepath, string localpath)
        {
            var objectName = remotepath;

            var downloadFilename = localpath;
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
    }
}
