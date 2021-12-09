using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Common
{

    /// <summary>
    /// 针对string字符串常用操作方法
    /// </summary>
    public class StringHelper
    {


        /// <summary>
        /// 生成一个订单号
        /// </summary>
        /// <returns></returns>
        public static string GetOrderNo(string sign)
        {
            Random ran = new();
            int RandKey = ran.Next(10000, 99999);


            string orderno = sign + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + RandKey;
            return orderno;
        }



        /// <summary>
        /// 移除字符串中的全部标点符号
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string RemovePunctuation(string text)
        {
            text = new string(text.Where(c => !char.IsPunctuation(c)).ToArray());

            return text;
        }



        /// <summary>
        /// 判断字符串中是否包含中文
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static bool IsContainsCN(string text)
        {
            Regex reg = new(@"[\u4e00-\u9fa5]");//正则表达式

            if (reg.IsMatch(text))
            {
                return true;
            }
            else
            {
                return false;
            }
        }



        /// <summary>
        /// 从传入的HTML代码中提取文本内容
        /// </summary>
        /// <param name="htmlText"></param>
        /// <returns></returns>
        public static string NoHtml(string htmlText)
        {
            if (!string.IsNullOrEmpty(htmlText))
            {
                //删除脚本

                htmlText = Regex.Replace(htmlText, @"<script[^>]*?>.*?</script>", "",

                RegexOptions.IgnoreCase);

                //删除HTML

                htmlText = Regex.Replace(htmlText, @"<(.[^>]*)>", "",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"([\r\n])[\s]+", "",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"-->", "", RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"<!--.*", "", RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"&(quot|#34);", "\"",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"&(amp|#38);", "&",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"&(lt|#60);", "<",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"&(gt|#62);", ">",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"&(nbsp|#160);", " ",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"&(iexcl|#161);", "\xa1",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"&(cent|#162);", "\xa2",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"&(pound|#163);", "\xa3",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"&(copy|#169);", "\xa9",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"&#(\d+);", "",

                RegexOptions.IgnoreCase);

                htmlText = htmlText.Replace("<", "");

                htmlText = htmlText.Replace(">", "");

                htmlText = htmlText.Replace("\r\n", "");

                htmlText = WebUtility.HtmlEncode(htmlText).Trim();

                return htmlText;
            }
            else
            {
                return htmlText;
            }
        }


        /// <summary>
        /// 过滤删除掉字符串中的 Emoji 表情
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string NoEmoji(string value)
        {

            string emojiUnicodeBytesStr = "[4848,14254,6032,7332,420,22732,350,6148,1690,1740,14833,14933,15033,15133,15233,15333,16933,17033,4035,20735,23735,23835,23935,24135,24235,24835,24935,25035,17037,17137,18237,19237,25137,25237,38,138,238,338,438,1438,1738,2438,2938,3238,3438,3538,3838,4238,4638,4738,5638,5738,5838,6438,6638,9538,9638,9938,10138,10238,10438,12338,12638,14638,14838,14938,15038,15138,15338,15538,15638,16038,16738,17638,17738,20038,20738,20938,21138,23338,24038,24138,24438,24738,24838,24938,239,839,939,1239,1339,1539,1839,2039,2239,2939,3339,5139,5239,6839,7139,9939,10039,16139,5241,5341,543,643,743,253255,480,490,500,510,520,530,540,550,560,570,5733,19436,3433,15150,15350,15254,1139,1039,2138,23438,24238,25038,25338,14738,24538,2735,24335,2635,24035,8043,19738,2038,16138,19638,4039,18938,19038,24338,12738,21238,7238,7338,7438,7538,7638,7738,7838,7938,8038,8138,8238,8338,20638,23335,23435,23535,23635,14939,15039,15139,8339,8439,8539,8739,8543,539,7639,7839,17639,19139,17138,17038,2743,2843,25437,25337,1332]";

            var emojiUnicodeBytesList = Json.JsonHelper.JsonToObject<List<int>>(emojiUnicodeBytesStr);

            foreach (var v in value)
            {
                var emojiUnicodeBytes = Encoding.Unicode.GetBytes(v.ToString()).ToList();

                var emojiUnicodeBytesInt = int.Parse(string.Join("", emojiUnicodeBytes));

                if (emojiUnicodeBytesList.Contains(emojiUnicodeBytesInt))
                {
                    value = value.Replace(v.ToString(), "");
                }
            }

            return value;
        }



        /// <summary>
        /// 对文本进行指定长度截取并添加省略号
        /// </summary>
        /// <param name="NeiRong"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string SubstringText(string NeiRong, int length)
        {
            //先对字符串做一次HTML解码
            NeiRong = HttpUtility.HtmlDecode(NeiRong);

            if (NeiRong.Length > length)
            {
                NeiRong = NeiRong[..length];

                NeiRong += "...";

                return NoHtml(NeiRong);
            }
            else
            {
                return NoHtml(NeiRong);
            }
        }



        /// <summary>
        /// 对字符串进行脱敏处理
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string TextStars(string text)
        {
            if (text.Length >= 3)
            {
                int group = text.Length / 3;

                string stars = text.Substring(group, group);

                string pstars = "";

                for (int i = 0; i < group; i++)
                {
                    pstars += "*";
                }

                text = text.Replace(stars, pstars);
            }
            else
            {

                string stars = text.Substring(1, 1);

                string pstars = "";

                for (int i = 0; i < 1; i++)
                {
                    pstars += "*";
                }

                text = text.Replace(stars, pstars);
            }

            return text;
        }


        /// <summary>
        /// Unicode转换中文
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string UnicodeToString(string text)
        {
            return Regex.Unescape(text);
        }




        /// <summary>
        /// 去掉字符串中的数字
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string RemoveNumber(string key)
        {
            return System.Text.RegularExpressions.Regex.Replace(key, @"\d", "");
        }



        /// <summary>
        /// 去掉字符串中的非数字
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string RemoveNotNumber(string key)
        {
            return System.Text.RegularExpressions.Regex.Replace(key, @"[^\d]*", "");
        }
    }
}
