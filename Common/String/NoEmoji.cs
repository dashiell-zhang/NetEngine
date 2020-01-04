using System;
using System.Collections.Generic;
using System.Text;

namespace Common.String
{

    /// <summary>
    /// 过滤删除掉字符串中的 Emoji 表情
    /// </summary>
    public class NoEmoji
    {

        public static string Run(string value)
        {
            foreach (var a in value)
            {
                byte[] bts = Encoding.UTF32.GetBytes(a.ToString());

                if (bts[0].ToString() == "253" && bts[1].ToString() == "255")
                {
                    value = value.Replace(a.ToString(), "");
                }

            }

            return value;
        }
    }
}
