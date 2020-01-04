using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Common.Crypto
{
    public class Md5
    {

        /// <summary>
        /// 获取字符串的 MD5 签名
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string GetMd5(string data)
        {
            MD5CryptoServiceProvider MD5 = new MD5CryptoServiceProvider();
            return BitConverter.ToString(MD5.ComputeHash(Encoding.GetEncoding("utf-8").GetBytes(data))).Replace("-", "").ToLower();
        }



        /// <summary>
        /// 获取字符串加 Token 后的 MD5 签名
        /// </summary>
        /// <param name="data"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string GetMd5(string data,string token)
        {
            string dataMD5 = data + token;
            MD5CryptoServiceProvider MD5 = new MD5CryptoServiceProvider();
            return BitConverter.ToString(MD5.ComputeHash(Encoding.GetEncoding("utf-8").GetBytes(dataMD5))).Replace("-", "").ToLower();
        }
    }
}
