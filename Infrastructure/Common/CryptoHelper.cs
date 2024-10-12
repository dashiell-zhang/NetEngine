using System.Security.Cryptography;
using System.Text;

namespace Common
{
    public class CryptoHelper
    {


        /// <summary>
        /// 使用 Aes 加密
        /// </summary>
        /// <param name="text">待加密文本</param>
        /// <param name="privateKey">密钥 16位采用Aes128、24位采用Aes192、32位采用Aes256</param>
        /// <param name="stringEncoding">返回结果的字符串编码类型 base64 或 hex</param>
        /// <returns></returns>
        public static string AesEncrypt(string text, string privateKey, string stringEncoding)
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(privateKey);
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;
            byte[] inputBuffers = Encoding.UTF8.GetBytes(text);
            ICryptoTransform cryptoTransform = aes.CreateEncryptor();
            byte[] results = cryptoTransform.TransformFinalBlock(inputBuffers, 0, inputBuffers.Length);

            switch (stringEncoding)
            {
                case "base64":
                    {
                        return Convert.ToBase64String(results);
                    }
                case "hex":
                    {
                        return Convert.ToHexString(results);
                    }
                default:
                    {
                        throw new ArgumentException("stringEncoding 无效，只能是 base64 或 hex");
                    }
            }
        }



        /// <summary>
        /// 使用 Aes 解密
        /// </summary>
        /// <param name="cipherText">加密文本</param>
        /// <param name="privateKey">密钥 16位采用Aes128、24位采用Aes192、32位采用Aes256</param>
        /// <param name="stringEncoding">加密文本的字符串编码类型 base64 或 hex</param>
        /// <returns></returns>
        public static string AesDecrypt(string cipherText, string privateKey, string stringEncoding)
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(privateKey);
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;
            byte[] inputBuffers;

            switch (stringEncoding)
            {
                case "base64":
                    {
                        inputBuffers = Convert.FromBase64String(cipherText);
                        break;
                    }
                case "hex":
                    {
                        inputBuffers = Convert.FromHexString(cipherText);
                        break;
                    }
                default:
                    {
                        throw new ArgumentException("stringEncoding 无效，只能是 base64 或 hex");
                    }
            }

            ICryptoTransform cryptoTransform = aes.CreateDecryptor();
            byte[] results = cryptoTransform.TransformFinalBlock(inputBuffers, 0, inputBuffers.Length);
            return Encoding.UTF8.GetString(results);
        }



        /// <summary>
        /// 使用 AesGcm 解密
        /// </summary>
        /// <param name="cipherText">已加密文本</param>
        /// <param name="privateKey">密钥 16位采用Aes128、24位采用Aes192、32位采用Aes256</param>
        /// <param name="nonce">随机值 长度必须是12</param>
        /// <param name="associatedText">相关文本</param>
        /// <param name="stringEncoding">已加密文本的字符串编码类型 base64 或 hex</param>
        /// <returns></returns>
        public static string AesGcmDecrypt(string cipherText, string privateKey, string nonce, string? associatedText, string stringEncoding)
        {
            var keyBytes = Encoding.UTF8.GetBytes(privateKey);
            var nonceBytes = Encoding.UTF8.GetBytes(nonce);
            var associatedBytes = associatedText == null ? null : Encoding.UTF8.GetBytes(associatedText);

            byte[] encryptedBytes;

            switch (stringEncoding)
            {
                case "base64":
                    {
                        encryptedBytes = Convert.FromBase64String(cipherText);
                        break;
                    }
                case "hex":
                    {
                        encryptedBytes = Convert.FromHexString(cipherText);
                        break;
                    }
                default:
                    {
                        throw new ArgumentException("stringEncoding 无效，只能是 base64 或 hex");
                    }
            }

            var cipherBytes = encryptedBytes[..^16];
            var tag = encryptedBytes[^16..];
            var decryptedData = new byte[cipherBytes.Length];
            using AesGcm cipher = new(keyBytes, tag.Length);
            cipher.Decrypt(nonceBytes, cipherBytes, tag, decryptedData, associatedBytes);
            return Encoding.UTF8.GetString(decryptedData);
        }



        /// <summary>
        /// 使用 AesGcm 加密
        /// </summary>
        /// <param name="text">待加密文本</param>
        /// <param name="privateKey">密钥 16位采用Aes128、24位采用Aes192、32位采用Aes256</param>
        /// <param name="nonce">随机值 长度必须是12</param>
        /// <param name="associatedText">附加数据</param>
        /// <param name="stringEncoding">加密结果的符串编码类型 base64 或 hex</param>
        /// <returns></returns>
        public static string AesGcmEncrypt(string text, string privateKey, string nonce, string? associatedText, string stringEncoding)
        {
            var keyBytes = Encoding.UTF8.GetBytes(privateKey);
            var nonceBytes = Encoding.UTF8.GetBytes(nonce);
            var associatedBytes = associatedText == null ? null : Encoding.UTF8.GetBytes(associatedText);

            var plainBytes = Encoding.UTF8.GetBytes(text);
            var cipherBytes = new byte[plainBytes.Length];

            var tag = new byte[16];
            using AesGcm cipher = new(keyBytes, tag.Length);
            cipher.Encrypt(nonceBytes, plainBytes, cipherBytes, tag, associatedBytes);

            var cipherWithTag = new byte[cipherBytes.Length + tag.Length];
            Buffer.BlockCopy(cipherBytes, 0, cipherWithTag, 0, cipherBytes.Length);
            Buffer.BlockCopy(tag, 0, cipherWithTag, cipherBytes.Length, tag.Length);

            switch (stringEncoding)
            {
                case "base64":
                    {
                        return Convert.ToBase64String(cipherWithTag);
                    }
                case "hex":
                    {
                        return Convert.ToHexString(cipherWithTag);
                    }
                default:
                    {
                        throw new ArgumentException("stringEncoding 无效，只能是 base64 或 hex");
                    }
            }
        }



        /// <summary>
        /// 获取字符串的 Base64 编码
        /// </summary>
        /// <param name="text">待编码的字符串</param>
        /// <returns></returns>
        public static string Base64Encode(string text)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        }



        /// <summary>
        /// 解码 Base64 字符串
        /// </summary>
        /// <param name="text">待解码的字符串</param>
        /// <returns></returns>
        public static string Base64Decode(string text)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(text));
        }



        /// <summary>
        /// MD5 摘要计算
        /// </summary>
        /// <param name="text">待计算摘要的文本</param>
        /// <returns></returns>
        public static string MD5HashData(string text)
        {
            return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(text)));
        }



        /// <summary>
        /// SHA1 摘要计算
        /// </summary>
        /// <param name="text">待计算摘要的文本</param>
        /// <param name="stringEncoding">摘要计算结果的字符串编码类型 base64 或 hex</param>
        /// <returns></returns>
        public static string SHA1HashData(string text, string stringEncoding = "hex")
        {
            switch (stringEncoding)
            {
                case "base64":
                    {
                        return Convert.ToBase64String(SHA1.HashData(Encoding.UTF8.GetBytes(text)));
                    }
                case "hex":
                    {
                        return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(text)));
                    }
                default:
                    {
                        throw new ArgumentException("stringEncoding 无效，只能是 base64 或 hex");
                    }
            }
        }



        /// <summary>
        /// SHA256 摘要计算
        /// </summary>
        /// <param name="text">待计算摘要的文本</param>
        /// <param name="stringEncoding">摘要计算结果的字符串编码类型 base64 或 hex</param>
        /// <returns></returns>
        public static string SHA256HashData(string text, string stringEncoding = "hex")
        {
            switch (stringEncoding)
            {
                case "base64":
                    {
                        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
                    }
                case "hex":
                    {
                        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
                    }
                default:
                    {
                        throw new ArgumentException("stringEncoding 无效，只能是 base64 或 hex");
                    }
            }
        }



        /// <summary>
        /// SHA384 摘要计算
        /// </summary>
        /// <param name="text">待计算摘要的文本</param>
        /// <param name="stringEncoding">摘要计算结果的字符串编码类型 base64 或 hex</param>
        /// <returns></returns>
        public static string SHA384HashData(string text, string stringEncoding = "hex")
        {
            switch (stringEncoding)
            {
                case "base64":
                    {
                        return Convert.ToBase64String(SHA384.HashData(Encoding.UTF8.GetBytes(text)));
                    }
                case "hex":
                    {
                        return Convert.ToHexString(SHA384.HashData(Encoding.UTF8.GetBytes(text)));
                    }
                default:
                    {
                        throw new ArgumentException("stringEncoding 无效，只能是 base64 或 hex");
                    }
            }
        }



        /// <summary>
        /// SHA512 摘要计算
        /// </summary>
        /// <param name="text">待计算摘要的文本</param>
        /// <param name="stringEncoding">摘要计算结果的字符串编码类型 base64 或 hex</param>
        /// <returns></returns>
        public static string SHA512HashData(string text, string stringEncoding = "hex")
        {
            switch (stringEncoding)
            {
                case "base64":
                    {
                        return Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes(text)));
                    }
                case "hex":
                    {
                        return Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(text)));
                    }
                default:
                    {
                        throw new ArgumentException("stringEncoding 无效，只能是 base64 或 hex");
                    }
            }
        }



        /// <summary>
        /// HMACMD5 摘要计算
        /// </summary>
        /// <param name="text">待计算摘要的文本</param>
        /// <param name="privateKey">私钥</param>
        /// <param name="stringEncoding">摘要计算结果的字符串编码类型 base64 或 hex</param>
        /// <returns></returns>
        public static string HMACMD5HashData(string text, string privateKey, string stringEncoding = "hex")
        {
            switch (stringEncoding)
            {
                case "base64":
                    {
                        return Convert.ToBase64String(HMACMD5.HashData(Encoding.UTF8.GetBytes(privateKey), Encoding.UTF8.GetBytes(text)));
                    }
                case "hex":
                    {
                        return Convert.ToHexString(HMACMD5.HashData(Encoding.UTF8.GetBytes(privateKey), Encoding.UTF8.GetBytes(text)));
                    }
                default:
                    {
                        throw new ArgumentException("stringEncoding 无效，只能是 base64 或 hex");
                    }
            }
        }



        /// <summary>
        /// HMACSHA1 摘要计算
        /// </summary>
        /// <param name="text">待计算摘要的文本</param>
        /// <param name="privateKey">私钥</param>
        /// <param name="stringEncoding">摘要计算结果的字符串编码类型 base64 或 hex</param>
        /// <returns></returns>
        public static string HMACSHA1HashData(string text, string privateKey, string stringEncoding = "hex")
        {
            switch (stringEncoding)
            {
                case "base64":
                    {
                        return Convert.ToBase64String(HMACSHA1.HashData(Encoding.UTF8.GetBytes(privateKey), Encoding.UTF8.GetBytes(text)));
                    }
                case "hex":
                    {
                        return Convert.ToHexString(HMACSHA1.HashData(Encoding.UTF8.GetBytes(privateKey), Encoding.UTF8.GetBytes(text)));
                    }
                default:
                    {
                        throw new ArgumentException("stringEncoding 无效，只能是 base64 或 hex");
                    }
            }
        }



        /// <summary>
        /// HMACSHA256 摘要计算
        /// </summary>
        /// <param name="text">待计算摘要的文本</param>
        /// <param name="privateKey">私钥</param>
        /// <param name="stringEncoding">摘要计算结果的字符串编码类型 base64 或 hex</param>
        /// <returns></returns>
        public static string HMACSHA256HashData(string text, string privateKey, string stringEncoding = "hex")
        {
            switch (stringEncoding)
            {
                case "base64":
                    {
                        return Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(privateKey), Encoding.UTF8.GetBytes(text)));
                    }
                case "hex":
                    {
                        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(privateKey), Encoding.UTF8.GetBytes(text)));
                    }
                default:
                    {
                        throw new ArgumentException("stringEncoding 无效，只能是 base64 或 hex");
                    }
            }
        }



        /// <summary>
        /// HMACSHA384 摘要计算
        /// </summary>
        /// <param name="text">待计算摘要的文本</param>
        /// <param name="privateKey">私钥</param>
        /// <param name="stringEncoding">摘要计算结果的字符串编码类型 base64 或 hex</param>
        /// <returns></returns>
        public static string HMACSHA384HashData(string text, string privateKey, string stringEncoding = "hex")
        {
            switch (stringEncoding)
            {
                case "base64":
                    {
                        return Convert.ToBase64String(HMACSHA384.HashData(Encoding.UTF8.GetBytes(privateKey), Encoding.UTF8.GetBytes(text)));
                    }
                case "hex":
                    {
                        return Convert.ToHexString(HMACSHA384.HashData(Encoding.UTF8.GetBytes(privateKey), Encoding.UTF8.GetBytes(text)));
                    }
                default:
                    {
                        throw new ArgumentException("stringEncoding 无效，只能是 base64 或 hex");
                    }
            }
        }



        /// <summary>
        /// HMACSHA512 摘要计算
        /// </summary>
        /// <param name="text">待计算摘要的文本</param>
        /// <param name="privateKey">私钥</param>
        /// <param name="stringEncoding">摘要计算结果的字符串编码类型 base64 或 hex</param>
        /// <returns></returns>
        public static string HMACSHA512HashData(string text, string privateKey, string stringEncoding = "hex")
        {
            switch (stringEncoding)
            {
                case "base64":
                    {
                        return Convert.ToBase64String(HMACSHA512.HashData(Encoding.UTF8.GetBytes(privateKey), Encoding.UTF8.GetBytes(text)));
                    }
                case "hex":
                    {
                        return Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(privateKey), Encoding.UTF8.GetBytes(text)));
                    }
                default:
                    {
                        throw new ArgumentException("stringEncoding 无效，只能是 base64 或 hex");
                    }
            }
        }




        /// <summary>
        /// RSA 签名计算
        /// </summary>
        /// <param name="privateKey">私钥</param>      
        /// <param name="content">待计算签名的文本</param>
        /// <param name="stringEncoding">签名结果的字符串编码类型 base64 或 hex</param>
        /// <param name="hashAlgorithm">签名算法 MD5 SHA1 SHA256 SHA512</param>
        /// <param name="signaturePadding">签名填充模式 Pss 或 Pkcs1</param>
        /// <remarks>新系统推荐签名算法至少采用SHA256，签名填充模式采用Pss，Pkcs1仅用于兼容老系统安全性不如Pss</remarks>
        /// <returns></returns>
        public static string RSASignData(string privateKey, string content, string stringEncoding, HashAlgorithmName hashAlgorithm, RSASignaturePadding signaturePadding)
        {
            privateKey = privateKey.Replace("\n", "").Replace("\r", "");

            using var rsa = RSA.Create();

        ImportInfo:
            if (privateKey.StartsWith('-') && privateKey.EndsWith('-'))
            {
                try
                {
                    rsa.ImportFromPem(privateKey);
                }
                catch
                {
                    privateKey = privateKey.Split("-").ToList().OrderByDescending(t => t.Length).First();

                    goto ImportInfo;
                }
            }
            else
            {
                byte[] keyData = Convert.FromBase64String(privateKey);

                try
                {
                    rsa.ImportRSAPrivateKey(keyData, out _);
                }
                catch
                {
                    try
                    {
                        rsa.ImportPkcs8PrivateKey(keyData, out _);
                    }
                    catch
                    {
                        throw new Exception("私钥导入初始化RSA失败");
                    }
                }
            }

            byte[] data = Encoding.UTF8.GetBytes(content);

            var signData = rsa.SignData(data, hashAlgorithm, signaturePadding);

            switch (stringEncoding)
            {
                case "base64":
                    {
                        return Convert.ToBase64String(signData);
                    }
                case "hex":
                    {
                        return Convert.ToHexString(signData);
                    }
                default:
                    {
                        throw new ArgumentException("stringEncoding 无效，只能是 base64 或 hex");
                    }
            }
        }



        /// <summary>
        /// RSA 签名验证
        /// </summary>
        /// <param name="publicKey">公钥</param>
        /// <param name="content">待验证签名的文本</param>
        /// <param name="signature">签名</param>
        /// <param name="stringEncoding">签名的字符串编码类型 base64 或 hex</param>
        /// <param name="hashAlgorithm">签名算法 MD5 SHA1 SHA256 SHA512</param>
        /// <param name="signaturePadding">签名填充模式 Pss 或 Pkcs1</param>
        /// <remarks>新系统推荐签名算法至少采用SHA256，签名填充模式采用Pss，Pkcs1仅用于兼容老系统安全性不如Pss</remarks>
        /// <returns></returns>
        public static bool RSAVerifyData(string publicKey, string content, string signature, string stringEncoding, HashAlgorithmName hashAlgorithm, RSASignaturePadding signaturePadding)
        {
            publicKey = publicKey.Replace("\n", "").Replace("\r", "");

            if (publicKey.StartsWith('-') && publicKey.EndsWith('-'))
            {
                publicKey = publicKey.Split("-").ToList().OrderByDescending(t => t.Length).First();
            }

            using var rsa = RSA.Create();

            rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKey), out _);

            var data = Encoding.UTF8.GetBytes(content);

            if (rsa != null)
            {

                byte[] signData;

                switch (stringEncoding)
                {
                    case "base64":
                        {
                            signData = Convert.FromBase64String(signature);
                            break;
                        }
                    case "hex":
                        {
                            signData = Convert.FromHexString(signature);
                            break;
                        }
                    default:
                        {
                            throw new ArgumentException("stringEncoding 无效，只能是 base64 或 hex");
                        }
                }

                return rsa.VerifyData(data, signData, hashAlgorithm, signaturePadding);

            }
            else
            {
                throw new Exception("RSA 初始化失败");
            }


        }



        /// <summary>
        /// RSA 加密
        /// </summary>
        /// <param name="publicKey">公钥</param>      
        /// <param name="content">待加密的文本</param>
        /// <param name="stringEncoding">加密结果的字符串编码类型 base64 或 hex</param>
        /// <param name="encryptionPadding">加密算法 Pkcs1 SHA1 SHA256 SHA512</param>
        /// <remarks>新系统推荐签名算法至少采用SHA256</remarks>
        /// <returns></returns>
        public static string RSAEncrypt(string publicKey, string content, string stringEncoding, RSAEncryptionPadding encryptionPadding)
        {
            publicKey = publicKey.Replace("\n", "").Replace("\r", "");

            if (publicKey.StartsWith('-') && publicKey.EndsWith('-'))
            {
                publicKey = publicKey.Split("-").ToList().OrderByDescending(t => t.Length).First();
            }

            using var rsa = RSA.Create();

            rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKey), out _);

            if (rsa != null)
            {

                byte[] data = Encoding.UTF8.GetBytes(content);

                var signData = rsa.Encrypt(data, encryptionPadding);

                switch (stringEncoding)
                {
                    case "base64":
                        {
                            return Convert.ToBase64String(signData);
                        }
                    case "hex":
                        {
                            return Convert.ToHexString(signData);
                        }
                    default:
                        {
                            throw new ArgumentException("stringEncoding 无效，只能是 base64 或 hex");
                        }
                }
            }
            else
            {
                throw new Exception("RSA 初始化失败");
            }
        }



        /// <summary>
        /// RSA 解密
        /// </summary>
        /// <param name="privateKey">私钥</param>      
        /// <param name="content">已加密的文本</param>
        /// <param name="stringEncoding">已加密文本的字符串编码类型 base64 或 hex</param>
        /// <param name="encryptionPadding">加密算法 Pkcs1 SHA1 SHA256 SHA512</param>
        /// <remarks>新系统推荐签名算法至少采用SHA256</remarks>
        /// <returns></returns>
        public static string RSADecrypt(string privateKey, string content, string stringEncoding, RSAEncryptionPadding encryptionPadding)
        {
            privateKey = privateKey.Replace("\n", "").Replace("\r", "");

            using var rsa = RSA.Create();

        ImportInfo:
            if (privateKey.StartsWith('-') && privateKey.EndsWith('-'))
            {
                try
                {
                    rsa.ImportFromPem(privateKey);
                }
                catch
                {
                    privateKey = privateKey.Split("-").ToList().OrderByDescending(t => t.Length).First();

                    goto ImportInfo;
                }
            }
            else
            {
                byte[] keyData = Convert.FromBase64String(privateKey);

                try
                {
                    rsa.ImportRSAPrivateKey(keyData, out _);
                }
                catch
                {
                    try
                    {
                        rsa.ImportPkcs8PrivateKey(keyData, out _);
                    }
                    catch
                    {
                        throw new Exception("私钥导入初始化RSA失败");
                    }
                }
            }


            byte[] contentData;

            switch (stringEncoding)
            {
                case "base64":
                    {
                        contentData = Convert.FromBase64String(content);
                        break;
                    }
                case "hex":
                    {
                        contentData = Convert.FromHexString(content);
                        break;
                    }
                default:
                    {
                        throw new ArgumentException("stringEncoding 无效，只能是 base64 或 hex");
                    }
            }

            return Encoding.UTF8.GetString(rsa.Decrypt(contentData, encryptionPadding));
        }

    }
}
