#if !BROWSER
using System.IO.Compression;

namespace Common;

/// <summary>
/// 提供文件、目录和ZIP归档处理能力
/// </summary>
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

            if (directory.LinkTarget != null)
            {
                directory.Delete();
                return true;
            }

            if (!directory.Exists)
            {
                return true;
            }

            if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                directory.Delete();
                return true;
            }

            NormalizeAttributes(directory);

            directory.Delete(true);

            return true;
        }

        catch
        {
            return false;
        }

        static void NormalizeAttributes(DirectoryInfo directory)
        {
            foreach (var item in directory.EnumerateFileSystemInfos())
            {
                if (item.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

                if (item is DirectoryInfo subDirectory)
                {
                    NormalizeAttributes(subDirectory);
                }
                else
                {
                    item.Attributes = FileAttributes.Normal;
                }
            }

            directory.Attributes = FileAttributes.Normal;
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

        string sourcePath = Path.GetFullPath(filePath);
        string targetPath = Path.GetFullPath(zipPath);

        if (Path.GetRelativePath(sourcePath, targetPath) == ".")
        {
            throw new ArgumentException("ZIP目标文件不能与待压缩文件相同", nameof(zipPath));
        }

        FileInfo fileInfo = new(sourcePath);

        string tempPath = Path.Combine(fileInfo.DirectoryName!, Guid.NewGuid() + "_temp");

        if (!Directory.Exists(tempPath))
        {
            Directory.CreateDirectory(tempPath);
        }

        try
        {
            fileInfo.CopyTo(Path.Combine(tempPath, fileInfo.Name));

            CompressDirectoryZip(tempPath, targetPath);
        }
        catch
        {
            DeleteDirectory(tempPath);
            throw;
        }

        if (!DeleteDirectory(tempPath))
        {
            throw new IOException($"临时目录清理失败：{tempPath}");
        }
    }


    /// <summary>
    /// 将指定目录压缩为Zip文件
    /// </summary>
    /// <param name="folderPath">文件夹地址 D:/1/ </param>
    /// <param name="zipPath">zip地址 D:/1.zip </param>
    public static void CompressDirectoryZip(string folderPath, string zipPath)
    {

        string sourcePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath));
        string targetPath = Path.GetFullPath(zipPath);
        string relativeTargetPath = Path.GetRelativePath(sourcePath, targetPath);

        if (!relativeTargetPath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) && relativeTargetPath != ".." && !Path.IsPathRooted(relativeTargetPath))
        {
            throw new ArgumentException("ZIP目标文件不能位于待压缩目录内部", nameof(zipPath));
        }

        string? targetDirectory = Path.GetDirectoryName(targetPath);

        if (targetDirectory == null)
        {
            throw new ArgumentException("ZIP目标路径无效", nameof(zipPath));
        }

        Directory.CreateDirectory(targetDirectory);
        string tempZipPath = Path.Combine(targetDirectory, Path.GetFileName(targetPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            ZipFile.CreateFromDirectory(sourcePath, tempZipPath, CompressionLevel.Optimal, false);
            File.Move(tempZipPath, targetPath, true);
        }
        finally
        {
            DeleteFile(tempZipPath);
        }
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

        ZipFile.ExtractToDirectory(zipPath, folderPath, true);
    }

}
#endif
