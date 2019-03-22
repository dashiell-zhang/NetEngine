using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace Methods.String
{
    public class JieQu
    {
        /// <summary>
        /// 对文本进行制定长度截取并添加省略号
        /// </summary>
        /// <param name="NeiRong"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string Run(string NeiRong, int length)
        {
            //先对字符串做一次HTML解码
            NeiRong = HttpUtility.HtmlDecode(NeiRong);

            if (NeiRong.Length > length)
            {
                NeiRong = NeiRong.Substring(0, length);

                NeiRong = NeiRong + "...";

                return NoHtml.Run(NeiRong);
            }
            else
            {
                return NoHtml.Run(NeiRong);
            }
        }
    }
}
