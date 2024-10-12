﻿namespace FileStorage
{
    public interface IFileStorage
    {


        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="localPath">本地文件路径</param>
        /// <param name="remotePath">远程文件路径</param>
        /// <param name="isPublicRead">是否支持公开访问</param>
        /// <param name="fileName">自定义下载文件名</param>
        /// <returns></returns>
        public bool FileUpload(string localPath, string remotePath, bool isPublicRead, string? fileName = null);



        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="remotePath">远程文件路径</param>
        /// <param name="localPath">本地文件路径</param>
        /// <returns></returns>
        public bool FileDownload(string remotePath, string localPath);



        /// <summary>
        /// 单个文件删除方法
        /// </summary>
        /// <param name="remotePath">远程文件地址</param>
        /// <returns></returns>
        public bool FileDelete(string remotePath);




        /// <summary>
        /// 获取文件临时访问URL
        /// </summary>
        /// <param name="remotePath">远程文件地址</param>
        /// <param name="expiry">失效时长</param>
        /// <param name="fileName">自定义下载文件名</param>
        /// <returns></returns>
        public string? GetFileTempURL(string remotePath, TimeSpan expiry, string? fileName = null);


    }
}
