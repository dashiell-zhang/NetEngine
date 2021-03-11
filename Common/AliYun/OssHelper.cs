using Aliyun.OSS;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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




        /// <summary>
        /// 单个文件删除方法
        /// </summary>
        /// <param name="remotePath">远程文件地址</param>
        /// <returns></returns>
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




        /// <summary>
        /// 文件批量删除方法
        /// </summary>
        /// <param name="remotePathList">远程文件地址集合</param>
        /// <returns></returns>
        public bool FileBatchDelete(List<string> remotePathList)
        {

            try
            {
                var client = new OssClient(endpoint, accessKeyId, accessKeySecret);

                var request = new DeleteObjectsRequest(bucketName, remotePathList, true);

                client.DeleteObjects(request);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Delete objects failed. {0}", ex.Message);
                return false;
            }
        }




        /// <summary>
        /// 创建存储空间（Bucket）
        /// </summary>
        /// <param name="bucketName"></param>
        public bool BucketCreate(string bucketName)
        {
            try
            {
                // 初始化OssClient。
                var client = new OssClient(endpoint, accessKeyId, accessKeySecret);

                var exist = client.DoesBucketExist(bucketName);

                if (exist == false)
                {
                    var request = new CreateBucketRequest(bucketName);

                    //设置存储空间访问权限ACL。
                    request.ACL = CannedAccessControlList.PublicRead;

                    //设置数据容灾类型。
                    request.DataRedundancyType = DataRedundancyType.LRS;

                    // 创建存储空间。
                    client.CreateBucket(request);
                }

                return true;

            }
            catch (Exception ex)
            {
                Console.WriteLine("Create bucket failed. {0}", ex.Message);

                return false;
            }
        }




        /// <summary>
        /// 删除存储空间（Bucket）
        /// </summary>
        /// <param name="bucketName"></param>
        /// <param name="isCoerce">是否强制删除</param>
        public bool BucketDelete(string bucketName, bool isCoerce = false)
        {
            try
            {

                // 初始化OssClient。
                var client = new OssClient(endpoint, accessKeyId, accessKeySecret);

                if (isCoerce)
                {

                    while (true)
                    {
                        var listObjectsRequest = new ListObjectsRequest(bucketName);

                        var result = client.ListObjects(listObjectsRequest);

                        var keys = result.ObjectSummaries.ToList().Select(t => t.Key).ToList();

                        if (keys.Count == 0)
                        {
                            break;
                        }

                        var request = new DeleteObjectsRequest(bucketName, keys, true);
                        client.DeleteObjects(request);
                    }

                }



                var exist = client.DoesBucketExist(bucketName);

                if (exist == true)
                {
                    client.DeleteBucket(bucketName);
                }

                return true;

            }
            catch (Exception ex)
            {
                Console.WriteLine("Delete bucket failed. {0}", ex.Message);

                return false;
            }
        }



    }
}
