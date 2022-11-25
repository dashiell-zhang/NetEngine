using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Data;
using System.Reflection;

namespace Common
{
    public class DataHelper
    {


        /// <summary>
        /// 将datatable 转换成 实体List
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="table"></param>
        /// <returns></returns>
        public static IList<T> DataTableToList<T>(DataTable table) where T : class
        {
            if (table.Rows.Count == 0)
            {
                return new List<T>();
            }
            else
            {

                IList<T> list = new List<T>();
                foreach (DataRow dr in table.Rows)
                {
                    T model = Activator.CreateInstance<T>();

                    foreach (DataColumn dc in dr.Table.Columns)
                    {
                        object drValue = dr[dc.ColumnName];

                        PropertyInfo? pi = model.GetType().GetProperty(dc.ColumnName);

                        if (pi != null && pi.CanWrite && (drValue != null && !Convert.IsDBNull(drValue)))
                        {
                            string piFullName = pi.PropertyType.FullName!;

                            if (piFullName.Contains("System.DateTime"))
                            {
                                if (piFullName.StartsWith("System.Nullable`1[[System.DateTime"))
                                {
                                    pi.SetValue(model, null, null);
                                }
                                else
                                {
                                    pi.SetValue(model, Convert.ToDateTime(drValue), null);
                                }
                            }
                            else if (piFullName.Contains("System.Boolean"))
                            {
                                pi.SetValue(model, Convert.ToBoolean(drValue), null);
                            }
                            else
                            {
                                pi.SetValue(model, drValue, null);
                            }

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
        public static IList<T> DataTableToListDisplayName<T>(DataTable table) where T : class
        {
            if (table.Rows.Count == 0)
            {
                return new List<T>();
            }
            else
            {

                IList<T> list = new List<T>();

                foreach (DataRow dr in table.Rows)
                {
                    T model = Activator.CreateInstance<T>();

                    foreach (DataColumn dc in dr.Table.Columns)
                    {
                        object drValue = dr[dc.ColumnName];

                        var properties = model.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).ToList();

                        var displayNamePI = properties.Where(p => p.CustomAttributes.Where(t => t.AttributeType.Name == "DisplayNameAttribute").Select(t => t.ConstructorArguments.Select(v => v.Value.ToString()).FirstOrDefault()).FirstOrDefault() == dc.ColumnName).FirstOrDefault();

                        PropertyInfo? pi = model.GetType().GetProperty(dc.ColumnName) ?? displayNamePI;

                        if (pi != null && pi.CanWrite && (drValue != null && !Convert.IsDBNull(drValue)))
                        {
                            string piFullName = pi.PropertyType.FullName!;

                            if (piFullName.Contains("System.DateTime"))
                            {
                                if (piFullName.StartsWith("System.Nullable`1[[System.DateTime"))
                                {
                                    pi.SetValue(model, null, null);
                                }
                                else
                                {
                                    pi.SetValue(model, Convert.ToDateTime(drValue), null);
                                }
                            }
                            else if (piFullName.Contains("System.Boolean"))
                            {
                                pi.SetValue(model, Convert.ToBoolean(drValue), null);
                            }
                            else
                            {
                                pi.SetValue(model, drValue, null);
                            }

                        }
                    }

                    list.Add(model);
                }
                return list;
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
                        column = new(name, pi.PropertyType);
                        dt.Columns.Add(column);
                    }

                    row[name] = pi.GetValue(t, null);
                }

                if (createColumn)
                {
                    createColumn = false;
                }

                dt.Rows.Add(row);
            }
            return dt;

        }



        /// <summary>  
        /// 将excel导入到datatable  
        /// </summary>  
        /// <param name="filePath">excel路径</param>  
        /// <param name="isColumnName">第一行是否是列名</param>  
        /// <returns>返回datatable</returns>  
        public static DataTable? ExcelToDataTable(string filePath, bool isColumnName)
        {
            DataTable? dataTable = null;
            FileStream? fs = null;
            IWorkbook? workbook = null;
            ISheet? sheet = null;
            int startRow = 0;
            try
            {
                using (fs = File.OpenRead(filePath))
                {
                    // 2007版本  
                    if (filePath.IndexOf(".xlsx") > 0)
                        workbook = new XSSFWorkbook(fs);
                    // 2003版本  
                    else if (filePath.IndexOf(".xls") > 0)
                        workbook = new HSSFWorkbook(fs);

                    if (workbook != null)
                    {
                        sheet = workbook.GetSheetAt(0);//读取第一个sheet，当然也可以循环读取每个sheet  
                        dataTable = new();
                        if (sheet != null)
                        {
                            int rowCount = sheet.LastRowNum;//总行数  
                            if (rowCount > 0)
                            {
                                IRow firstRow = sheet.GetRow(0);//第一行  
                                int cellCount = firstRow.LastCellNum;//列数  

                                DataColumn column;
                                ICell cell;
                                //构建datatable的列  
                                if (isColumnName)
                                {
                                    startRow = 1;//如果第一行是列名，则从第二行开始读取  
                                    for (int i = firstRow.FirstCellNum; i < cellCount; ++i)
                                    {
                                        cell = firstRow.GetCell(i);
                                        if (cell != null)
                                        {
                                            if (cell.StringCellValue != null)
                                            {
                                                column = new(cell.StringCellValue);
                                                dataTable.Columns.Add(column);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    for (int i = firstRow.FirstCellNum; i < cellCount; ++i)
                                    {
                                        column = new("column" + (i + 1));
                                        dataTable.Columns.Add(column);
                                    }
                                }

                                //填充行  
                                for (int i = startRow; i <= rowCount; ++i)
                                {
                                    IRow row = sheet.GetRow(i);
                                    if (row == null || row!.Cells.Count == 0) continue;

                                    DataRow dataRow = dataTable.NewRow();
                                    for (int j = row.FirstCellNum; j < cellCount; ++j)
                                    {
                                        cell = row.GetCell(j);
                                        if (cell == null)
                                        {
                                            dataRow[j] = "";
                                        }
                                        else
                                        {
                                            //CellType(Unknown = -1,Numeric = 0,String = 1,Formula = 2,Blank = 3,Boolean = 4,Error = 5,)  
                                            switch (cell.CellType)
                                            {
                                                case CellType.Blank:
                                                    dataRow[j] = "";
                                                    break;
                                                case CellType.Numeric:
                                                    //NPOI中数字和日期都是NUMERIC类型的，这里对其进行判断是否是日期类型
                                                    if (DateUtil.IsCellDateFormatted(cell))//日期类型
                                                    {
                                                        dataRow[j] = cell.DateCellValue;
                                                    }
                                                    else//其他数字类型
                                                    {
                                                        dataRow[j] = cell.NumericCellValue;
                                                    }
                                                    break;
                                                case CellType.String:
                                                    dataRow[j] = cell.StringCellValue;
                                                    break;
                                            }
                                        }
                                    }
                                    dataTable.Rows.Add(dataRow);
                                }
                            }
                        }
                    }
                }
                return dataTable;
            }
            catch (Exception)
            {
                fs?.Close();
                return null;
            }
        }




        /// <summary>
        /// 将 List 数据转换为 Excel 文件流
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <returns></returns>
        public static byte[] ListToExcel<T>(List<T> list) where T : notnull, new()
        {

            //创建Excel文件的对象
            XSSFWorkbook book = new();
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

            foreach (var item in list)
            {
                int i = list.IndexOf(item);
                IRow rowtemp = sheet1.CreateRow(i + 1);

                dict = PropertyHelper.GetProperties(item);
                int d = 0;
                foreach (var it in dict)
                {

                    rowtemp.CreateCell(d).SetCellValue(it.Value != null ? it.Value.ToString() : "");
                    d++;
                }
            }

            using MemoryStream ms = new();
            book.Write(ms);
            var byteData = ms.ToArray();
            return byteData;

        }



        /// <summary>
        /// 将 List 数据转换为 Excel 文件流(使用DisplayName)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <returns></returns>
        public static byte[] ListToExcelDispalyName<T>(List<T> list) where T : notnull, new()
        {

            //创建Excel文件的对象
            XSSFWorkbook book = new();
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

            foreach (var item in list)
            {
                int i = list.IndexOf(item);
                IRow rowtemp = sheet1.CreateRow(i + 1);

                dict = PropertyHelper.GetProperties(item);
                int d = 0;
                foreach (var it in dict)
                {

                    rowtemp.CreateCell(d).SetCellValue(it.Value != null ? it.Value.ToString() : "");
                    d++;
                }
            }


            using MemoryStream ms = new();
            book.Write(ms);
            var byteData = ms.ToArray();
            return byteData;

        }
    }
}
