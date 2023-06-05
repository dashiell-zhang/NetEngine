using System.Security.Cryptography;
using System.Text;

namespace Common
{
    public class CryptoHelper
    {



        /// <summary>
        /// 通过AES加密字符串
        /// </summary>
        /// <param name="param">字符串</param>
        /// <param name="skey">密钥</param>
        /// <remarks>16位采用AES128,24位采用AES192,32位采用AES256</remarks>
        /// <returns></returns>
        public static string AESEncode(string text, string privateKey)
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(privateKey);
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;
            byte[] inputBuffers = Encoding.UTF8.GetBytes(text);
            ICryptoTransform cryptoTransform = aes.CreateEncryptor();
            byte[] results = cryptoTransform.TransformFinalBlock(inputBuffers, 0, inputBuffers.Length);
            return Convert.ToHexString(results);
        }



        /// <summary>
        /// 通过AES解密字符串
        /// </summary>
        /// <param name="param">字符串</param>
        /// <param name="skey">密钥</param>
        /// <remarks>16位采用AES128,24位采用AES192,32位采用AES256</remarks>
        /// <returns></returns>
        public static string AESDecode(string text, string privateKey)
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(privateKey);
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;
            byte[] inputBuffers = Convert.FromHexString(text);
            ICryptoTransform cryptoTransform = aes.CreateDecryptor();
            byte[] results = cryptoTransform.TransformFinalBlock(inputBuffers, 0, inputBuffers.Length);
            return Encoding.UTF8.GetString(results);
        }



        /// <summary>
        /// 获取字符串的 MD5 签名
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string GetMD5(string text)
        {
            return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(text)));
        }



        /// <summary>
        /// 通过Base64加密字符串
        /// </summary>
        /// <param name="text">要加密的字符串</param>
        /// <returns></returns>
        public static string Base64Encode(string text)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        }



        /// <summary>
        /// 通过Base64解密字符串
        /// </summary>
        /// <param name="text">被加密的字符串</param>
        /// <returns></returns>
        public static string Base64Decode(string text)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(text));
        }



        /// <summary>
        /// SHA256 签名计算
        /// </summary>
        /// <param name="srcString">The string to be encrypted</param>
        /// <returns></returns>
        public static string GetSHA256(string text)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        }



        /// <summary>
        /// SHA512 签名计算
        /// </summary>
        /// <param name="srcString">The string to be encrypted</param>
        /// <returns></returns>
        public static string GetSHA512(string text)
        {
            return Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(text)));
        }



        /// <summary>
        /// 使用私钥对待签名串进行SHA256 with RSA签名，并对签名结果进行Hex编码得到签名值
        /// </summary>
        /// <param name="content"></param>
        /// <param name="privateKey"></param>
        /// <returns></returns>
        public static string SHA256withRSAToHex(string content, string privateKey)
        {
            byte[] keyData = Convert.FromBase64String(privateKey);

            using var rsa = RSA.Create();

            rsa.ImportPkcs8PrivateKey(keyData, out _);

            byte[] data = Encoding.UTF8.GetBytes(content);

            var signData = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            return Convert.ToHexString(signData);
        }



        /// <summary>
        /// 使用私钥对待签名串进行SHA256 with RSA签名，并对签名结果进行Base64编码得到签名值
        /// </summary>
        /// <param name="content"></param>
        /// <param name="privateKey"></param>
        /// <returns></returns>
        public static string SHA256withRSAToBase64(string content, string privateKey)
        {
            byte[] keyData = Convert.FromBase64String(privateKey);

            using var rsa = RSA.Create();

            rsa.ImportPkcs8PrivateKey(keyData, out _);

            byte[] data = Encoding.UTF8.GetBytes(content);

            var signData = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            return Convert.ToBase64String(signData);
        }



    }
}
