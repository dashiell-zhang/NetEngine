using System;
using System.Collections.Generic;
using System.IO;
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
    }
}
