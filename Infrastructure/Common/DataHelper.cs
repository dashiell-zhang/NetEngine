using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Reflection;

#if !BROWSER
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
#endif

namespace Common;

/// <summary>
/// 提供DataTable、实体集合和Excel之间的数据转换能力
/// </summary>
public partial class DataHelper
{

    /// <summary>
    /// 将datatable 转换成 实体List
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="table"></param>
    /// <returns></returns>
    public static List<T> DataTableToList<T>(DataTable table) where T : class, new()
    {
        if (table.Rows.Count == 0)
        {
            return [];
        }
        else
        {

            List<T> list = [];
            foreach (DataRow dr in table.Rows)
            {
                T model = Activator.CreateInstance<T>();

                foreach (DataColumn dc in dr.Table.Columns)
                {
                    object drValue = dr[dc.ColumnName];

                    PropertyInfo? pi = model.GetType().GetProperty(dc.ColumnName);

                    if (pi != null && pi.CanWrite && (drValue != null && !Convert.IsDBNull(drValue)))
                    {
                        SetPropertyValue(model, pi, drValue);
                    }
                }

                list.Add(model);
            }
            return list;
        }

    }


    /// <summary>
    /// 将datatable 转换成 实体List(加入实体DisplayName表头匹配 )
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="table"></param>
    /// <returns></returns>
    public static List<T> DataTableToListDisplayName<T>(DataTable table) where T : class, new()
    {
        if (table.Rows.Count == 0)
        {
            return [];
        }
        else
        {

            List<T> list = [];

            foreach (DataRow dr in table.Rows)
            {
                T model = Activator.CreateInstance<T>();

                foreach (DataColumn dc in dr.Table.Columns)
                {
                    object drValue = dr[dc.ColumnName];

                    var properties = model.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).ToList();

                    var displayNamePI = properties.Where(p => p.CustomAttributes.Where(t => t.AttributeType.Name == "DisplayNameAttribute").Select(t => t.ConstructorArguments.Select(v => v.Value?.ToString()).FirstOrDefault()).FirstOrDefault() == dc.ColumnName).FirstOrDefault();

                    PropertyInfo? pi = model.GetType().GetProperty(dc.ColumnName) ?? displayNamePI;

                    if (pi != null && pi.CanWrite && (drValue != null && !Convert.IsDBNull(drValue)))
                    {
                        SetPropertyValue(model, pi, drValue);
                    }
                }

                list.Add(model);
            }
            return list;
        }

    }


    /// <summary>
    /// 通过枚举的描述获取枚举值
    /// </summary>
    /// <param name="enumType"></param>
    /// <param name="description"></param>
    /// <returns></returns>
    private static object? GetEnumValueFromDescription(Type enumType, string description)
    {
        foreach (var field in enumType.GetFields())
        {
            if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attribute && attribute.Description == description)
                return Enum.Parse(enumType, field.Name);
        }

        return null;
    }


    /// <summary>
    /// 设置 DataRow 值到实体属性
    /// </summary>
    private static void SetPropertyValue<T>(T model, PropertyInfo pi, object drValue) where T : class
    {
        Type propertyType = pi.PropertyType;
        Type targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        bool isNullable = Nullable.GetUnderlyingType(propertyType) != null || !propertyType.IsValueType;
        string drValueStr = $"{drValue}".Trim();

        if (string.IsNullOrWhiteSpace(drValueStr))
        {
            if (isNullable)
            {
                pi.SetValue(model, null, null);
            }

            return;
        }

        object? value;

        if (targetType == typeof(string))
        {
            value = drValueStr;
        }
        else if (targetType.IsEnum)
        {
            value = GetEnumValue(targetType, drValueStr);
        }
        else if (targetType == typeof(Guid))
        {
            value = Guid.Parse(drValueStr);
        }
        else if (targetType == typeof(DateOnly))
        {
            value = drValue is DateTime dateTime ? DateOnly.FromDateTime(dateTime) : DateOnly.Parse(drValueStr, CultureInfo.CurrentCulture);
        }
        else if (targetType == typeof(TimeOnly))
        {
            value = drValue is DateTime dateTime ? TimeOnly.FromDateTime(dateTime) : TimeOnly.Parse(drValueStr, CultureInfo.CurrentCulture);
        }
        else if (targetType == typeof(DateTimeOffset))
        {
            value = drValue is DateTime dateTime ? new DateTimeOffset(dateTime) : DateTimeOffset.Parse(drValueStr, CultureInfo.CurrentCulture);
        }
        else
        {
            value = Convert.ChangeType(drValue, targetType, CultureInfo.CurrentCulture);
        }

        pi.SetValue(model, value, null);

        static object GetEnumValue(Type enumType, string value)
        {
            if (int.TryParse(value, out int drValueInt))
            {
                return Enum.ToObject(enumType, drValueInt);
            }

            if (Enum.TryParse(enumType, value, out object? enumValueTemp))
            {
                return enumValueTemp!;
            }

            var enumValue = GetEnumValueFromDescription(enumType, value);

            if (enumValue != null)
            {
                return enumValue;
            }

            throw new Exception($"无法转换枚举：{value} => {enumType.FullName}");
        }
    }


    /// <summary>
    /// 实体List 转 datatable
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <returns></returns>
    public static DataTable? ListToDataTable<T>(IList<T> list)
        where T : class
    {
        if (list == null || list.Count <= 0)
        {
            return null;
        }
        DataTable dt = new(typeof(T).Name);
        DataColumn column;
        DataRow row;

        PropertyInfo[] myPropertyInfo = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        int length = myPropertyInfo.Length;
        bool createColumn = true;

        foreach (T t in list)
        {
            if (t == null)
            {
                continue;
            }

            row = dt.NewRow();
            for (int i = 0; i < length; i++)
            {
                PropertyInfo pi = myPropertyInfo[i];
                string name = pi.Name;
                if (createColumn)
                {
                    column = new(name, Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType);
                    dt.Columns.Add(column);
                }

                row[name] = pi.GetValue(t, null) ?? DBNull.Value;
            }

            if (createColumn)
            {
                createColumn = false;
            }

            dt.Rows.Add(row);
        }
        return dt;

    }

#if !BROWSER
    /// <summary>
    /// 将excel导入到datatable
    /// </summary>
    /// <param name="filePath">excel路径</param>
    /// <param name="isHaveColumnName">是否包含列名</param>
    /// <returns>返回datatable</returns>
    public static DataTable? ExcelToDataTable(string filePath, bool isHaveColumnName)
    {
        string extension = Path.GetExtension(filePath);

        using var fs = File.OpenRead(filePath);
        using IWorkbook workbook = extension.ToLowerInvariant() switch
        {
            ".xlsx" => new XSSFWorkbook(fs),
            ".xls" => new HSSFWorkbook(fs),
            _ => throw new ArgumentException("文件格式不支持，仅支持.xls和.xlsx格式", nameof(filePath))
        };

        if (workbook.NumberOfSheets == 0)
        {
            return null;
        }

        ISheet sheet = workbook.GetSheetAt(0);
        IRow? firstRow = sheet.GetRow(0);

        if (firstRow == null)
        {
            return new DataTable();
        }

        int cellCount = GetEffectiveColumnCount(firstRow);
        DataTable dataTable = new();
        List<int> sourceColumnIndexes = [];
        DataFormatter formatter = new(CultureInfo.CurrentCulture);

        for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
        {
            string columnName;

            if (isHaveColumnName)
            {
                var headerCell = firstRow.GetCell(cellIndex);
                columnName = headerCell == null ? string.Empty : formatter.FormatCellValue(headerCell).Trim();

                if (string.IsNullOrWhiteSpace(columnName))
                {
                    continue;
                }
            }
            else
            {
                columnName = $"column{cellIndex + 1}";
            }

            if (dataTable.Columns.Contains(columnName))
            {
                throw new InvalidDataException($"Excel表头存在重复列：{columnName}");
            }

            dataTable.Columns.Add(new DataColumn(columnName, typeof(object)));
            sourceColumnIndexes.Add(cellIndex);
        }

        int startRow = isHaveColumnName ? 1 : 0;

        for (int rowIndex = startRow; rowIndex <= sheet.LastRowNum; rowIndex++)
        {
            IRow? row = sheet.GetRow(rowIndex);

            if (row == null || !sourceColumnIndexes.Any(t => IsCellHasValue(row.GetCell(t))))
            {
                continue;
            }

            DataRow dataRow = dataTable.NewRow();

            for (int dataColumnIndex = 0; dataColumnIndex < sourceColumnIndexes.Count; dataColumnIndex++)
            {
                var cell = row.GetCell(sourceColumnIndexes[dataColumnIndex]);
                dataRow[dataColumnIndex] = GetCellValue(cell);
            }

            dataTable.Rows.Add(dataRow);
        }

        return dataTable;

        static int GetEffectiveColumnCount(IRow row)
        {
            int lastCell = row.LastCellNum;
            while (lastCell > 0)
            {
                var cell = row.GetCell(lastCell - 1);
                if (cell != null && IsCellHasValue(cell))
                {
                    break;
                }
                lastCell--;
            }
            return lastCell;
        }

        //判断单元格是否包含有效值
        static bool IsCellHasValue(ICell? cell)
        {
            if (cell == null)
            {
                return false;
            }

            return cell.CellType switch
            {
                CellType.Blank => false,
                CellType.String => !string.IsNullOrWhiteSpace(cell.StringCellValue),
                CellType.Formula => !string.IsNullOrWhiteSpace(cell.ToString()),
                _ => true
            };
        }


        static object GetCellValue(ICell? cell)
        {
            if (cell == null)
            {
                return string.Empty;
            }

            return cell.CellType switch
            {
                CellType.Boolean => cell.BooleanCellValue,
                CellType.Blank => string.Empty,
                CellType.Numeric => DateUtil.IsCellDateFormatted(cell) ? cell.DateCellValue.HasValue ? cell.DateCellValue.Value : string.Empty : cell.NumericCellValue,
                CellType.Formula => GetFormulaCellValue(cell),
                CellType.String => cell.StringCellValue,
                CellType.Error => string.Empty,
                _ => cell.ToString() ?? string.Empty
            };
        }


        /// <summary>
        /// 获取公式单元格缓存结果
        /// </summary>
        static object GetFormulaCellValue(ICell cell)
        {
            return cell.CachedFormulaResultType switch
            {
                CellType.Boolean => cell.BooleanCellValue,
                CellType.Numeric => DateUtil.IsCellDateFormatted(cell) ? cell.DateCellValue.HasValue ? cell.DateCellValue.Value : "" : cell.NumericCellValue,
                CellType.String => cell.StringCellValue,
                CellType.Blank => "",
                CellType.Error => "",
                _ => cell.ToString() ?? ""
            };
        }
    }


    /// <summary>
    /// 将 List 数据转换为 Excel 文件流
    /// </summary>
    public static byte[] ListToExcel<T>(List<T> list) where T : notnull, new()
    {
        //创建Excel文件的对象
        using XSSFWorkbook book = new();

        //添加一个sheet
        ISheet sheet1 = book.CreateSheet("Sheet1");

        //给sheet1添加第一行的头部标题
        IRow row1 = sheet1.CreateRow(0);

        T model = new();
        var dict = PropertyHelper.GetProperties(model);

        int x = 0;
        foreach (var item in dict)
        {
            row1.CreateCell(x).SetCellValue(item.Key.ToString());
            x++;
        }

        //将数据逐步写入sheet1各个行
        int rowIndex = 1;
        foreach (var item in list)
        {
            IRow rowtemp = sheet1.CreateRow(rowIndex);

            dict = PropertyHelper.GetProperties(item);
            int d = 0;
            foreach (var it in dict)
            {
                ToExcelSetValue(rowtemp, d, it.Value);
                d++;
            }
            rowIndex++;
        }

        using MemoryStream ms = new();
        book.Write(ms);
        var byteData = ms.ToArray();
        return byteData;
    }


    /// <summary>
    /// 将 List 数据转换为 Excel 文件流(使用DisplayName)
    /// </summary>
    public static byte[] ListToExcelDisplayName<T>(List<T> list) where T : notnull, new()
    {
        //创建Excel文件的对象
        using XSSFWorkbook book = new();

        //添加一个sheet
        ISheet sheet1 = book.CreateSheet("Sheet1");

        //给sheet1添加第一行的头部标题
        IRow row1 = sheet1.CreateRow(0);

        T model = new();
        var dict = PropertyHelper.GetPropertiesDisplayName(model);

        int x = 0;
        foreach (var item in dict)
        {
            row1.CreateCell(x).SetCellValue(item.Key.ToString());
            x++;
        }

        //将数据逐步写入sheet1各个行
        int rowIndex = 1;
        foreach (var item in list)
        {
            IRow rowtemp = sheet1.CreateRow(rowIndex);

            dict = PropertyHelper.GetProperties(item);
            int d = 0;
            foreach (var it in dict)
            {
                ToExcelSetValue(rowtemp, d, it.Value);
                d++;
            }
            rowIndex++;
        }

        using MemoryStream ms = new();
        book.Write(ms);
        var byteData = ms.ToArray();
        return byteData;
    }


    /// <summary>
    /// 将 List 数据转换为指定模板 Excel 文件流
    /// </summary>
    public static byte[] ListToExcel<T>(List<T> list, ExcelTemplate excelTemplate) where T : notnull, new()
    {
        using XSSFWorkbook book = new();
        ISheet sheet = book.CreateSheet("Sheet1");

        // 添加表头
        IRow headerRow = sheet.CreateRow(0);

        for (int columnIndex = 0; columnIndex < excelTemplate.ColumnList.Count; columnIndex++)
        {
            var columnProperty = excelTemplate.ColumnList[columnIndex];

            headerRow.CreateCell(columnIndex).SetCellValue(columnProperty.Title);
        }

        // 添加数据
        int rowIndex = 1;
        foreach (var item in list)
        {
            IRow dataRow = sheet.CreateRow(rowIndex);
            int cellIndex = 0;
            foreach (var column in excelTemplate.ColumnList)
            {
                if (column.Field.Contains('.'))
                {
                    string[] fieldList = column.Field.Split('.');
                    object? propertyValue = item;

                    foreach (string field in fieldList)
                    {
                        if (propertyValue == null)
                        {
                            break;
                        }

                        var property = propertyValue?.GetType().GetProperties()
                      .FirstOrDefault(p => p.Name.Equals(field, StringComparison.OrdinalIgnoreCase));

                        if (property != null)
                        {
                            propertyValue = property.GetValue(propertyValue);
                        }
                        else
                        {
                            throw new Exception(column.Field + "字段不存在");
                        }
                    }

                    ToExcelSetValue(dataRow, cellIndex, propertyValue);
                }
                else
                {
                    var property = typeof(T).GetProperties()
                   .FirstOrDefault(p => p.Name.Equals(column.Field, StringComparison.OrdinalIgnoreCase));

                    if (property != null)
                    {
                        var propertyValue = property.GetValue(item);

                        ToExcelSetValue(dataRow, cellIndex, propertyValue);
                    }
                    else
                    {
                        throw new Exception(column.Field + "字段不存在");
                    }
                }
                cellIndex++;
            }
            rowIndex++;
        }

        using MemoryStream ms = new();
        book.Write(ms);
        return ms.ToArray();

    }


    /// <summary>
    /// 表示Excel导出模板
    /// </summary>
    public class ExcelTemplate
    {

        /// <summary>
        /// 列的集合
        /// </summary>
        public List<ColumnProperty> ColumnList { get; set; } = [];


        /// <summary>
        /// 表示Excel模板列
        /// </summary>
        public class ColumnProperty
        {

            /// <summary>
            /// 标题
            /// </summary>
            public string Title { get; set; } = string.Empty;


            /// <summary>
            /// 字段名
            /// </summary>
            public string Field { get; set; } = string.Empty;

        }
    }


    /// <summary>
    /// 将属性值写入Excel单元格
    /// </summary>
    /// <param name="dataRow">目标数据行</param>
    /// <param name="cellIndex">目标单元格索引</param>
    /// <param name="propertyValue">待写入的属性值</param>
    private static void ToExcelSetValue(IRow dataRow, int cellIndex, object? propertyValue)
    {
        if (propertyValue != null)
        {
            var cell = dataRow.CreateCell(cellIndex);

            if (propertyValue.GetType() == typeof(double))
            {
                cell.SetCellValue((double)propertyValue);
            }
            else if (propertyValue.GetType() == typeof(int))
            {
                cell.SetCellValue((int)propertyValue);
            }
            else if (propertyValue.GetType() == typeof(uint))
            {
                cell.SetCellValue((uint)propertyValue);
            }
            else if (propertyValue.GetType() == typeof(long))
            {
                long value = (long)propertyValue;

                if (value is >= -999999999999999 and <= 999999999999999)
                {
                    cell.SetCellValue((double)value);
                }
                else
                {
                    cell.SetCellValue(value.ToString(CultureInfo.InvariantCulture));
                }
            }
            else if (propertyValue.GetType() == typeof(ulong))
            {
                ulong value = (ulong)propertyValue;

                if (value <= 999999999999999)
                {
                    cell.SetCellValue((double)value);
                }
                else
                {
                    cell.SetCellValue(value.ToString(CultureInfo.InvariantCulture));
                }
            }
            else if (propertyValue.GetType() == typeof(decimal))
            {
                decimal value = (decimal)propertyValue;
                string decimalText = value.ToString(CultureInfo.InvariantCulture);
                string significantDigits = decimalText.TrimStart('-').Replace(".", "").TrimStart('0').TrimEnd('0');

                if (significantDigits.Length <= 15)
                {
                    cell.SetCellValue((double)value);
                }
                else
                {
                    cell.SetCellValue(decimalText);
                }
            }
            else if (propertyValue.GetType() == typeof(float))
            {
                cell.SetCellValue((float)propertyValue);
            }
            else if (propertyValue.GetType() == typeof(DateTimeOffset))
            {
                DateTimeOffset chinaTime = TimeZoneInfo.ConvertTime(((DateTimeOffset)propertyValue).ToUniversalTime(), TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"));
                cell.SetCellValue(chinaTime.ToString("yyyy/MM/dd HH:mm:ss"));
            }
            else if (propertyValue.GetType() == typeof(DateTime))
            {
                cell.SetCellValue(((DateTime)propertyValue).ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture));
            }
            else if (propertyValue.GetType() == typeof(bool))
            {
                cell.SetCellValue((bool)propertyValue);
            }
            else
            {
                cell.SetCellValue(propertyValue.ToString());
            }
        }
        else
        {
            dataRow.CreateCell(cellIndex).SetCellValue("");
        }
    }
#endif
}
