using System.IO;
using System.Net;

namespace Common.IO
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
        /// <param name="filepath">保存路径，以 / 结束，否则将取最后一个 / 之前的路径, / 之后的当作自定义文件名前缀</param>
        /// <param name="filename">保存文件名称,不传则自动通过 url 获取名称</param>
        /// <returns></returns>
        public static string DownloadFile(string url, string filepath, string filename = null)
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
                    //如果路径结尾不是 / 则说明尾端可能是自定义的文件名前缀

                    var lastindex = filepath.Length - filepath.LastIndexOf("/");

                    if (lastindex == 1)
                    {
                        Directory.CreateDirectory(filepath);
                    }
                    else
                    {
                        string temp = filepath.Substring(0, filepath.LastIndexOf("/"));
                        Directory.CreateDirectory(temp);

                    }
                }

                WebClient webClient = new WebClient();

                //添加来源属性，解决部分资源防盗链伪验证
                webClient.Headers.Add(HttpRequestHeader.Referer, "");

                string fullpath = filepath + filename;

                //下载文件
                webClient.DownloadFile(url, fullpath);

                return fullpath;
            }
            catch
            {
                return null;
            }
        }



        /// <summary>
        /// 获取指定文件的大小
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetFileSize(string path)
        {
            FileInfo fileInfo = null;
            fileInfo = new System.IO.FileInfo(path);

            string m_strSize = "";

            long FactSize = fileInfo.Length;

            if (FactSize < 1024.00)
            {
                m_strSize = FactSize.ToString("F2") + " Byte";
            }
            else if (FactSize >= 1024.00 && FactSize < 1048576)
            {
                m_strSize = (FactSize / 1024.00).ToString("F2") + " K";
            }
            else if (FactSize >= 1048576 && FactSize < 1073741824)
            {
                m_strSize = (FactSize / 1024.00 / 1024.00).ToString("F2") + " M";
            }
            else if (FactSize >= 1073741824)
            {
                m_strSize = (FactSize / 1024.00 / 1024.00 / 1024.00).ToString("F2") + " G";
            }

            return m_strSize;
        }
    }
}
