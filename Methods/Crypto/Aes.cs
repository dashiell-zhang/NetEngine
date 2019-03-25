using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Methods.Crypto
{
    public class Aes
    {
        public static byte[] hexStringToByte(string hex)
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

        public static string byte2HexString(byte[] b)
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


        public static string Encode(string param, string skey)
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

        public static string Decode(string param, string skey)
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
    }
}
