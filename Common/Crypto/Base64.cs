using System;
using System.Collections.Generic;
using System.Text;

namespace Common.Crypto
{
    public class Base64
    {

        /// <summary>
        /// 通过Base64加密字符串
        /// </summary>
        /// <param name="text">要加密的字符串</param>
        /// <returns></returns>
        public static string Encode(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(bytes);
        }




        /// <summary>
        /// 通过Base64解密字符串
        /// </summary>
        /// <param name="text">被加密的字符串</param>
        /// <returns></returns>
        public static string Decode(string text)
        {
            byte[] bytes = Convert.FromBase64String(text);
            return Encoding.UTF8.GetString(bytes);
        }

    }
}
