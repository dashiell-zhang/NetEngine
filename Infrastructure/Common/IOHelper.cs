using System.IO.Compression;

namespace Common
{
    public class IOHelper
    {

        /// <summary>
        /// 删除指定文件
        /// </summary>
        /// <param name="path">文件路径</param>
        public static bool DeleteFile(string path)
        {
            try
            {
                FileInfo file = new(path);
                if (file.Exists)
                {
                    //将文件属性设置为普通,如：只读文件设置为普通
                    file.Attributes = FileAttributes.Normal;

                    file.Delete();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }



        /// <summary>
        /// 删除指定文件夹
        /// </summary>
        /// <param name="path">文件夹路径</param>
        /// <returns></returns>
        public static bool DeleteDirectory(string path)
        {
            try
            {
                DirectoryInfo directory = new(path);
                if (directory.Exists)
                {
                    //将文件夹属性设置为普通,如：只读文件夹设置为普通
                    directory.Attributes = FileAttributes.Normal;

                    directory.Delete(true);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }



        /// <summary>
        /// 下载远程文件保存到本地
        /// </summary>
        /// <param name="url">文件URL</param>
        /// <param name="folderPath">保存文件夹路径</param>
        /// <param name="fileName">保存文件名称,不传则自动通过 url 获取名称</param>
        /// <returns></returns>
        public static string? DownloadFile(string url, string folderPath, string? fileName = null)
        {
            try
            {
                fileName ??= System.Web.HttpUtility.UrlDecode(Path.GetFileName(url));

                //检查目标路径文件夹是否存在不存在则创建
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                using HttpClient client = new();
                client.DefaultRequestVersion = new("2.0");

                using var httpResponse = client.GetAsync(url).Result;
                string filePath = Path.Combine(folderPath, fileName);

                File.WriteAllBytes(filePath, httpResponse.Content.ReadAsByteArrayAsync().Result);

                return filePath;
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
            FileInfo fileInfo = new(path);
            return FileLengthToString(fileInfo.Length);
        }



        /// <summary>
        /// 文件Length值转String
        /// </summary>
        /// <param name="fileLength"></param>
        /// <returns></returns>
        public static string FileLengthToString(long fileLength)
        {

            string m_strSize = "";

            if (fileLength < 1024.00)
            {
                m_strSize = fileLength.ToString("F2") + " Byte";
            }
            else if (fileLength >= 1024.00 && fileLength < 1048576)
            {
                m_strSize = (fileLength / 1024.00).ToString("F2") + " K";
            }
            else if (fileLength >= 1048576 && fileLength < 1073741824)
            {
                m_strSize = (fileLength / 1024.00 / 1024.00).ToString("F2") + " M";
            }
            else if (fileLength >= 1073741824)
            {
                m_strSize = (fileLength / 1024.00 / 1024.00 / 1024.00).ToString("F2") + " G";
            }

            return m_strSize;
        }



        /// <summary>
        /// 获取文件夹下所有文件
        /// </summary>
        /// <param name="folderPath">文件夹路径</param>
        public static List<string> GetFolderAllFiles(string folderPath, bool includeSubfolders = false)
        {
            List<string> list = [];

            DirectoryInfo directoryInfo = new(folderPath);
            foreach (FileInfo info in directoryInfo.GetFiles())
            {
                list.Add(info.FullName);
            }

            if (includeSubfolders)
            {
                foreach (DirectoryInfo info in directoryInfo.GetDirectories())
                {
                    list.AddRange(GetFolderAllFiles(info.FullName, includeSubfolders));
                }
            }



            return list;
        }



        /// <summary>
        /// 将指定文件压缩为Zip文件
        /// </summary>
        /// <param name="filePath">文件地址 D:/1.txt </param>
        /// <param name="zipPath">zip地址 D:/1.zip </param>
        public static void CompressFileZip(string filePath, string zipPath)
        {

            FileInfo fileInfo = new(filePath);

            string tempPath = Path.Combine(fileInfo.DirectoryName!, Guid.NewGuid() + "_temp");

            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }

            fileInfo.CopyTo(Path.Combine(tempPath, fileInfo.Name));

            CompressDirectoryZip(tempPath, zipPath);

            DeleteDirectory(tempPath);
        }


        /// <summary>
        /// 将指定目录压缩为Zip文件
        /// </summary>
        /// <param name="folderPath">文件夹地址 D:/1/ </param>
        /// <param name="zipPath">zip地址 D:/1.zip </param>
        public static void CompressDirectoryZip(string folderPath, string zipPath)
        {

            DirectoryInfo directoryInfo = new(zipPath);

            if (directoryInfo.Parent != null)
            {
                directoryInfo = directoryInfo.Parent;
            }

            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }

            ZipFile.CreateFromDirectory(folderPath, zipPath, CompressionLevel.Optimal, false);
        }



        /// <summary>
        /// 解压Zip文件到指定目录
        /// </summary>
        /// <param name="zipPath">zip地址 D:/1.zip</param>
        /// <param name="folderPath">文件夹地址 D:/1/</param>
        public static void DecompressZip(string zipPath, string folderPath)
        {
            DirectoryInfo directoryInfo = new(folderPath);

            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }

            ZipFile.ExtractToDirectory(zipPath, folderPath);
        }


    }
}
