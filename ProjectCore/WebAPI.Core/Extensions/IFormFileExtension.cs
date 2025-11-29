using Common;

namespace WebAPI.Core.Extensions;
public static class IFormFileExtension
{

    private static string baseDirectory = AppContext.BaseDirectory;


    /// <summary>
    /// 从Excel中提取List数据
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="file"></param>
    /// <returns></returns>
    /// <exception cref="CustomException"></exception>
    public static List<T> ExcelToList<T>(this IFormFile file) where T : class
    {
        var fileExtension = Path.GetExtension(file.FileName)?.ToLower();

        if (file.Length > 0 && fileExtension == ".xlsx")
        {
            var tempPath = Path.Combine(baseDirectory, "files");

            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }

            var fullPath = Path.Combine(tempPath, string.Format("{0}{1}", Guid.NewGuid(), fileExtension));

            try
            {
                using (FileStream fs = File.Create(fullPath))
                {
                    file.CopyTo(fs);
                    fs.Flush();
                }

                var dataTable = DataHelper.ExcelToDataTable(fullPath, true);

                if (dataTable != null)
                {
                    var dataList = DataHelper.DataTableToListDisplayName<T>(dataTable);

                    return dataList;
                }
                else
                {
                    throw new CustomException($"文件内容解析失败");
                }
            }
            finally
            {
                IOHelper.DeleteFile(fullPath);
            }
        }
        else
        {
            throw new CustomException($"文件格式不正确或文件内容为空");
        }
    }
}
