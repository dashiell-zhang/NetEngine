using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Common
{
    public static class CryptoHelper
    {

        private static byte[] hexStringToByte(string hex)
        {
            int target_length = hex.Length >> 1;
            byte[] result = new byte[target_length];
            char[] achar = hex.ToCharArray();
            for (int i = 0, j = 0; i < target_length; i++)
            {
                char c = achar[j++];
                if (c >= '0' && c <= '9')
                {
                    result[i] = (byte)((c - '0') << 4);
                }
                else if (c >= 'a' && c <= 'f')
                {
                    result[i] = (byte)((c - 'a' + 10) << 4);
                }
                else if (c >= 'A' && c <= 'F')
                {
                    result[i] = (byte)((c - 'A' + 10) << 4);
                }
                else
                {
                    return result;
                }
                c = achar[j++];

                if (c >= '0' && c <= '9')
                {
                    result[i] |= (byte)(c - '0');
                }
                else if (c >= 'a' && c <= 'f')
                {
                    result[i] |= (byte)(c - 'a' + 10);
                }
                else if (c >= 'A' && c <= 'F')
                {
                    result[i] |= (byte)(c - 'A' + 10);
                }
                else
                {
                    return result;
                }
            }
            return result;
        }

        private static string byte2HexString(byte[] b)
        {
            char[] hex = {'0', '1', '2', '3', '4', '5', '6', '7',
                      '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'};
            char[] newChar = new char[b.Length * 2];
            for (int i = 0; i < b.Length; i++)
            {
                newChar[2 * i] = hex[(b[i] & 0xf0) >> 4];
                newChar[2 * i + 1] = hex[b[i] & 0xf];
            }
            return new string(newChar);
        }


        /// <summary>
        /// AES 加密
        /// </summary>
        /// <param name="param">字符串</param>
        /// <param name="skey">密钥</param>
        /// <returns></returns>
        public static string AESEncode(string param, string skey)
        {
            byte[] key = hexStringToByte(skey.ToLower());
            AesCryptoServiceProvider aesProvider = new AesCryptoServiceProvider();
            aesProvider.Key = key;
            aesProvider.Mode = CipherMode.ECB;
            aesProvider.Padding = PaddingMode.PKCS7;
            byte[] inputBuffers = Encoding.UTF8.GetBytes(param);
            ICryptoTransform cryptoTransform = aesProvider.CreateEncryptor();
            byte[] results = cryptoTransform.TransformFinalBlock(inputBuffers, 0, inputBuffers.Length);
            aesProvider.Clear();
            return byte2HexString(results);

        }



        /// <summary>
        /// AES 解密
        /// </summary>
        /// <param name="param">字符串</param>
        /// <param name="skey">密钥</param>
        /// <returns></returns>
        public static string AESDecode(string param, string skey)
        {
            try
            {
                byte[] key = hexStringToByte(skey.ToLower());
                AesCryptoServiceProvider aesProvider = new AesCryptoServiceProvider();
                aesProvider.Key = key;
                aesProvider.Mode = CipherMode.ECB;
                aesProvider.Padding = PaddingMode.PKCS7;
                byte[] inputBuffers = hexStringToByte(param);
                ICryptoTransform cryptoTransform = aesProvider.CreateDecryptor();
                byte[] results = cryptoTransform.TransformFinalBlock(inputBuffers, 0, inputBuffers.Length);
                aesProvider.Clear();
                return Encoding.UTF8.GetString(results);
            }
            catch
            {
                return param;
            }
        }



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
        public static string GetMd5(string data, string token)
        {
            string dataMD5 = data + token;
            MD5CryptoServiceProvider MD5 = new MD5CryptoServiceProvider();
            return BitConverter.ToString(MD5.ComputeHash(Encoding.GetEncoding("utf-8").GetBytes(dataMD5))).Replace("-", "").ToLower();
        }



        /// <summary>
        /// 通过Base64加密字符串
        /// </summary>
        /// <param name="text">要加密的字符串</param>
        /// <returns></returns>
        public static string Base64Encode(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(bytes);
        }




        /// <summary>
        /// 通过Base64解密字符串
        /// </summary>
        /// <param name="text">被加密的字符串</param>
        /// <returns></returns>
        public static string Base64Decode(string text)
        {
            byte[] bytes = Convert.FromBase64String(text);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
