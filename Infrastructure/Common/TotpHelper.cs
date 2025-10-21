using System.Security.Cryptography;
using System.Text;

namespace Common
{

    public class TotpHelper
    {

        /// <summary> 支持的哈希算法类型（RFC 6238 标准） </summary>
        public enum Algo { Sha1, Sha256, Sha512 }

        /// <summary> 默认密钥长度（20 字节 = 160 bit） </summary>
        public const int DefaultSecretBytes = 20;

        /// <summary> 默认验证码位数（6 位） </summary>
        public const int DefaultDigits = 6;

        /// <summary> 默认时间步长（30 秒） </summary>
        public const int DefaultPeriod = 30;


        /// <summary>
        /// 生成随机 Base32 编码的密钥（每个用户唯一保存）
        /// </summary>
        public static string GenerateBase32Secret(int secretBytes = DefaultSecretBytes) => Base32Encode(RandomNumberGenerator.GetBytes(secretBytes));


        /// <summary>
        /// 构造 otpauth:// URL（用于二维码展示，兼容 Google/MS Authenticator）
        /// </summary>
        public static string BuildOtpAuthUrl(string issuer, string accountName, string base32Secret, int digits = DefaultDigits, int period = DefaultPeriod, Algo algo = Algo.Sha1)
        {

            if (digits is < 6 or > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(digits), "验证码位数必须介于 6 到 10 位之间。");
            }

            if (period <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(period), "时间步长（period）必须大于 0 秒。");
            }

            string encIssuer = Uri.EscapeDataString(issuer);
            string encAccount = Uri.EscapeDataString(accountName);
            string algoName = algo.ToString().ToUpperInvariant();

            return $"otpauth://totp/{encIssuer}:{encAccount}?secret={base32Secret}&issuer={encIssuer}&digits={digits}&period={period}&algorithm={algoName}";
        }


        /// <summary>
        /// 生成当前 TOTP 验证码（一般用于调试或对拍）
        /// </summary>
        public static string GenerateCode(string base32Secret, DateTimeOffset? now = null, int digits = DefaultDigits, int period = DefaultPeriod, Algo algo = Algo.Sha1)
        {
            var key = Base32Decode(base32Secret);
            long counter = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / period;
            return ComputeHotp(key, counter, digits, algo);
        }


        /// <summary>
        /// 验证用户输入的 TOTP 验证码（默认容差 ±1 步 = ±30 秒）
        /// </summary>
        public static bool VerifyCode(string base32Secret, string code, int allowedDrift = 1, int digits = DefaultDigits, int period = DefaultPeriod, Algo algo = Algo.Sha1)
        {
            if (code.Length != digits || code.Any(c => c < '0' || c > '9'))
            {
                return false;
            }

            var key = Base32Decode(base32Secret);
            long center = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / period;

            for (long i = -allowedDrift; i <= allowedDrift; i++)
            {
                var candidate = ComputeHotp(key, unchecked(center + i), digits, algo);
                if (FixedTimeEquals(candidate, code))
                {
                    return true;
                }
            }
            return false;
        }



        /// <summary>
        /// 计算 HOTP 核心逻辑（TOTP 只是基于时间计算 counter）
        /// 使用 TryComputeHash(ReadOnlySpan<byte>, Span<byte>, out int) 零堆分配。
        /// </summary>
        private static string ComputeHotp(byte[] key, long counter, int digits, Algo algo)
        {
            if (digits is < 6 or > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(digits), "验证码位数（digits）必须在 6 到 10 位之间。");
            }


            // 8 字节大端序计数器（栈上）
            Span<byte> counterBytes = stackalloc byte[8];
            for (int i = 7; i >= 0; i--)
            {
                counterBytes[i] = (byte)(counter & 0xFF);
                counter >>= 8;
            }

            // 输出缓冲区（SHA1=20, SHA256=32, SHA512=64）
            int hashLen = algo switch
            {
                Algo.Sha256 => 32,
                Algo.Sha512 => 64,
                _ => 20,
            };

            Span<byte> macBuffer = stackalloc byte[64];
            Span<byte> mac = macBuffer[..hashLen];   // ✅ IDE0057 建议写法

            using (HashAlgorithm h = algo switch
            {
                Algo.Sha256 => new HMACSHA256(key),
                Algo.Sha512 => new HMACSHA512(key),
                _ => new HMACSHA1(key),
            })
            {
                if (!h.TryComputeHash(counterBytes, mac, out int written) || written != hashLen)
                {
                    throw new CryptographicException("计算 HMAC 哈希值失败。");
                }
            }

            int offset = mac[^1] & 0x0F;
            int bin =
                ((mac[offset] & 0x7F) << 24) |
                ((mac[offset + 1] & 0xFF) << 16) |
                ((mac[offset + 2] & 0xFF) << 8) |
                (mac[offset + 3] & 0xFF);

            int mod = Pow10(digits);
            int otp = bin % mod;
            return otp.ToString().PadLeft(digits, '0');
        }


        /// <summary> 整数版 10ⁿ （避免 double 精度问题） </summary>
        private static int Pow10(int n)
        {
            int p = 1;
            for (int i = 0; i < n; i++)
            {
                p *= 10;
            }
            return p;
        }


        /// <summary> 固定时间比较，防止时间侧信道攻击 </summary>
        private static bool FixedTimeEquals(string a, string b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            int diff = 0;

            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }

            return diff == 0;
        }


        /// <summary> 将字节数组编码为 Base32 字符串 </summary>
        private static string Base32Encode(byte[] data)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            StringBuilder result = new((data.Length + 4) / 5 * 8);
            int buffer = data[0];
            int next = 1, bitsLeft = 8;

            while (bitsLeft > 0 || next < data.Length)
            {
                if (bitsLeft < 5)
                {
                    if (next < data.Length)
                    {
                        buffer = (buffer << 8) | (data[next++] & 0xFF);
                        bitsLeft += 8;
                    }
                    else
                    {
                        int pad = 5 - bitsLeft;
                        buffer <<= pad;
                        bitsLeft += pad;
                    }
                }

                int index = (buffer >> (bitsLeft - 5)) & 0x1F;
                bitsLeft -= 5;
                result.Append(alphabet[index]);
            }

            return result.ToString();
        }


        /// <summary> 将 Base32 字符串解码为 byte[]（忽略空白与 '='） </summary>
        private static byte[] Base32Decode(string base32)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            int buffer = 0, bitsLeft = 0;
            using var ms = new MemoryStream();

            foreach (char raw in base32.ToUpperInvariant())
            {
                if (raw == '=' || char.IsWhiteSpace(raw))
                    continue;

                int val = alphabet.IndexOf(raw);
                if (val < 0)
                {
                    throw new FormatException($"无效的 Base32 字符：'{raw}'。");
                }

                buffer = (buffer << 5) | val;
                bitsLeft += 5;

                if (bitsLeft >= 8)
                {
                    bitsLeft -= 8;
                    ms.WriteByte((byte)((buffer >> bitsLeft) & 0xFF));
                }
            }

            return ms.ToArray();
        }
    }
}
