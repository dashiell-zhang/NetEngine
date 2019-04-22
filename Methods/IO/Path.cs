using System;
using System.Collections.Generic;
using System.Text;

namespace Methods.IO
{
    public class AppPath
    {

        /// <summary>
        /// 获取网站物理根目录
        /// </summary>
        /// <returns></returns>
        public static string GetPath()
        {
            return AppContext.BaseDirectory;
        }
    }
}
