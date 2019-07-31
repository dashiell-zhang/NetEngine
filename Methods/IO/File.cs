using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Methods.IO
{
    public class File
    {

        /// <summary>
        /// 删除指定文件,删除后如果文件夹也为空同时删除文件夹
        /// </summary>
        /// <param name="Path"></param>
        public static void Delete(string path)
        {
            try
            {
                FileInfo file = new FileInfo(path);
                if (file.Exists)//判断文件是否存在
                {
                    file.Attributes = FileAttributes.Normal;//将文件属性设置为普通,比方说只读文件设置为普通
                    file.Delete();//删除文件
                }
                ////判断文件夹是否为空,为空则删除
                int s = path.LastIndexOf("/");
                path = path.Substring(0, s);
                if (Directory.GetFileSystemEntries(path).Length == 0) //判断文件夹为空,空则删除
                {
                    Directory.Delete(path);
                }
            }
            catch
            {

            }
        }






        /// <summary>
        /// 下载远程文件保存到本地
        /// </summary>
        /// <param name="url">文件URL</param>
        /// <param name="filepath">保存路径，以 \ 结束</param>
        /// <param name="filename">保存文件名称,不传则自动通过 url 获取名称</param>
        /// <returns></returns>
        public static bool DownloadFile(string url, string filepath, string filename = null)
        {
            try
            {
                if (filename == null)
                {
                    filename = System.IO.Path.GetFileName(url);
                }

                //检查目标路径文件夹是否存在不存在则创建
                if (!Directory.Exists(filepath))
                {
                    Directory.CreateDirectory(filepath);
                }

                WebClient webClient = new WebClient();


                //下载文件
                webClient.DownloadFile(url, filepath + filename);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
