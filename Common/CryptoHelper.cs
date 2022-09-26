using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
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
            using var md5 = MD5.Create();
            return Convert.ToHexString(md5.ComputeHash(Encoding.UTF8.GetBytes(text)));
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
            using SHA256 sha256 = SHA256.Create();
            return Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }



        /// <summary>
        /// SHA512 签名计算
        /// </summary>
        /// <param name="srcString">The string to be encrypted</param>
        /// <returns></returns>
        public static string GetSHA512(string text)
        {
            using SHA512 sha512 = SHA512.Create();
            return Convert.ToHexString(sha512.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }



        /// <summary>
        /// Java RSA 私钥转换为 DotNet 格式
        /// </summary>
        /// <param name="privateKey">私钥内容（不要前后缀）</param>
        /// <returns></returns>
        public static string RSAPrivateKeyJava2DotNet(string privateKey)
        {
            RsaPrivateCrtKeyParameters privateKeyParam = (RsaPrivateCrtKeyParameters)PrivateKeyFactory.CreateKey(Convert.FromBase64String(privateKey));

            return string.Format("<RSAKeyValue><Modulus>{0}</Modulus><Exponent>{1}</Exponent><P>{2}</P><Q>{3}</Q><DP>{4}</DP><DQ>{5}</DQ><InverseQ>{6}</InverseQ><D>{7}</D></RSAKeyValue>",
                Convert.ToBase64String(privateKeyParam.Modulus.ToByteArrayUnsigned()),
                Convert.ToBase64String(privateKeyParam.PublicExponent.ToByteArrayUnsigned()),
                Convert.ToBase64String(privateKeyParam.P.ToByteArrayUnsigned()),
                Convert.ToBase64String(privateKeyParam.Q.ToByteArrayUnsigned()),
                Convert.ToBase64String(privateKeyParam.DP.ToByteArrayUnsigned()),
                Convert.ToBase64String(privateKeyParam.DQ.ToByteArrayUnsigned()),
                Convert.ToBase64String(privateKeyParam.QInv.ToByteArrayUnsigned()),
                Convert.ToBase64String(privateKeyParam.Exponent.ToByteArrayUnsigned()));
        }



        /// <summary>
        /// 获取数据经过sha256，经过 rsa 加密之后的 hex 值
        /// </summary>
        /// <param name="contentForSign"></param>
        /// <param name="privateKey"></param>
        /// <remarks>Hex.encode(RSAWithSHA256(message)）</remarks>
        /// <returns></returns>
        public static string HexRSASha256Sign(string contentForSign, string privateKey)
        {
            //转换成适用于.Net的秘钥
            var netKey = RSAPrivateKeyJava2DotNet(privateKey);
            RSACryptoServiceProvider rsa = new();
            rsa.FromXmlString(netKey);
            //创建一个空对象
            RSACryptoServiceProvider rsaClear = new();
            var paras = rsa.ExportParameters(true);
            rsaClear.ImportParameters(paras);
            //签名返回
            using var sha256 = SHA256.Create();
            var signData = rsa.SignData(Encoding.UTF8.GetBytes(contentForSign), sha256);

            return Convert.ToHexString(signData);
        }



    }
}
