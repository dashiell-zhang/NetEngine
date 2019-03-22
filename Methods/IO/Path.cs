using System;
using System.Collections.Generic;
using System.Text;

namespace Methods.IO
{
    public class Path
    {

        /// <summary>
        /// 获取网站物理根目录
        /// </summary>
        /// <returns></returns>
        public static string SitePath()
        {
            string AppPath = "";
            //HttpContext HttpCurrent = HttpContext.Current;
            //if (HttpCurrent != null)
            //{
            //    AppPath = HttpCurrent.Server.MapPath("~");
            //}
            //else
            //{
            //    AppPath = AppDomain.CurrentDomain.BaseDirectory;
            //    if (Regex.Match(AppPath, @"\\$", RegexOptions.Compiled).Success)
            //        AppPath = AppPath.Substring(0, AppPath.Length - 1);
            //}
            return AppPath;
        }
    }
}
