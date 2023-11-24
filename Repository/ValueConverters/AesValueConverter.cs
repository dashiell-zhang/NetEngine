using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Security.Cryptography;
using System.Text;

namespace Repository.ValueConverters
{
    internal static class AesValueConverter
    {
        private const string privateKey = "TU3rlwQ4XNYonhgExwOSq1RCxYAUzlgw";


        private static readonly Aes aes;
        private static readonly ICryptoTransform encryptorTransform;
        private static readonly ICryptoTransform decryptorTransform;


        internal static readonly ValueConverter<string, string> aesConverter;



        static AesValueConverter()
        {
            aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(privateKey);
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;
            encryptorTransform = aes.CreateEncryptor();
            decryptorTransform = aes.CreateDecryptor();

            aesConverter = new(v => AesEncrypt(v), v => AesDecrypt(v));
        }



        private static string AesEncrypt(string text)
        {
            byte[] inputBuffers = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(encryptorTransform.TransformFinalBlock(inputBuffers, 0, inputBuffers.Length));
        }


        private static string AesDecrypt(string cipherText)
        {
            try
            {
                byte[] inputBuffers = Convert.FromBase64String(cipherText);
                return Encoding.UTF8.GetString(decryptorTransform.TransformFinalBlock(inputBuffers, 0, inputBuffers.Length));
            }
            catch
            {
                return cipherText;
            }
        }

    }
}
