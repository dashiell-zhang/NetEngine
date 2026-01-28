using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Common;

/// <summary>
/// 针对string字符串常用操作方法
/// </summary>
public partial class StringHelper
{

    /// <summary>
    /// 过滤删除掉字符串中的 全部标点符号
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static string RemovePunctuation(string text)
    {
        text = new string([.. text.Where(c => !char.IsPunctuation(c))]);

        return text;
    }


    /// <summary>
    /// 判断字符串中是否包含中文
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static bool IsContainsCN(string text)
    {
        Regex reg = RegexIsContainsCN();//正则表达式

        if (reg.IsMatch(text))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    [GeneratedRegex(@"[\u4E00-\u9FFF]")]
    private static partial Regex RegexIsContainsCN();


    /// <summary>
    /// 判断字符串中是否全部为中文
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static bool IsAllCN(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            string t = text.Substring(i, 1);

            var isCN = IsContainsCN(t);

            if (isCN == false)
            {
                return false;
            }
        }

        return true;
    }


    /// <summary>
    /// 过滤删除掉字符串中的 HTML 标签
    /// </summary>
    /// <param name="htmlText"></param>
    /// <returns></returns>
    public static string RemoveHtml(string htmlText)
    {
        if (!string.IsNullOrEmpty(htmlText))
        {
            //删除脚本
            htmlText = RegexRemoveHtml1().Replace(htmlText, "");

            //删除HTML

            htmlText = RegexRemoveHtml2().Replace(htmlText, "");

            htmlText = RegexRemoveHtml3().Replace(htmlText, "");

            htmlText = RegexRemoveHtml4().Replace(htmlText, "");

            htmlText = RegexRemoveHtml5().Replace(htmlText, "");

            htmlText = RegexRemoveHtml6().Replace(htmlText, "\"");

            htmlText = RegexRemoveHtml7().Replace(htmlText, "&");

            htmlText = RegexRemoveHtml8().Replace(htmlText, "<");

            htmlText = RegexRemoveHtml9().Replace(htmlText, ">");

            htmlText = RegexRemoveHtml10().Replace(htmlText, " ");

            htmlText = RegexRemoveHtml11().Replace(htmlText, "\xa1");

            htmlText = RegexRemoveHtml12().Replace(htmlText, "\xa2");

            htmlText = RegexRemoveHtml13().Replace(htmlText, "\xa3");

            htmlText = RegexRemoveHtml14().Replace(htmlText, "\xa9");

            htmlText = RegexRemoveHtml15().Replace(htmlText, "");

            htmlText = htmlText.Replace("<", "");

            htmlText = htmlText.Replace(">", "");

            htmlText = htmlText.Replace("\r\n", "");

            htmlText = WebUtility.HtmlEncode(htmlText).Trim();

            return htmlText;
        }
        else
        {
            return htmlText;
        }
    }

    #region RegexRemoveHtml

    [GeneratedRegex(@"<script[^>]*?>.*?</script>", RegexOptions.IgnoreCase, "zh-CN")]
    private static partial Regex RegexRemoveHtml1();


    [GeneratedRegex(@"<(.[^>]*)>", RegexOptions.IgnoreCase, "zh-CN")]
    private static partial Regex RegexRemoveHtml2();


    [GeneratedRegex(@"([\r\n])[\s]+", RegexOptions.IgnoreCase, "zh-CN")]
    private static partial Regex RegexRemoveHtml3();


    [GeneratedRegex(@"-->", RegexOptions.IgnoreCase, "zh-CN")]
    private static partial Regex RegexRemoveHtml4();


    [GeneratedRegex(@"<!--.*", RegexOptions.IgnoreCase, "zh-CN")]
    private static partial Regex RegexRemoveHtml5();


    [GeneratedRegex(@"&(quot|#34);", RegexOptions.IgnoreCase, "zh-CN")]
    private static partial Regex RegexRemoveHtml6();


    [GeneratedRegex(@"&(amp|#38);", RegexOptions.IgnoreCase, "zh-CN")]
    private static partial Regex RegexRemoveHtml7();
    [GeneratedRegex(@"&(lt|#60);", RegexOptions.IgnoreCase, "zh-CN")]
    private static partial Regex RegexRemoveHtml8();


    [GeneratedRegex(@"&(gt|#62);", RegexOptions.IgnoreCase, "zh-CN")]
    private static partial Regex RegexRemoveHtml9();


    [GeneratedRegex(@"&(nbsp|#160);", RegexOptions.IgnoreCase, "zh-CN")]
    private static partial Regex RegexRemoveHtml10();


    [GeneratedRegex(@"&(iexcl|#161);", RegexOptions.IgnoreCase, "zh-CN")]
    private static partial Regex RegexRemoveHtml11();


    [GeneratedRegex(@"&(cent|#162);", RegexOptions.IgnoreCase, "zh-CN")]
    private static partial Regex RegexRemoveHtml12();


    [GeneratedRegex(@"&(pound|#163);", RegexOptions.IgnoreCase, "zh-CN")]
    private static partial Regex RegexRemoveHtml13();


    [GeneratedRegex(@"&(copy|#169);", RegexOptions.IgnoreCase, "zh-CN")]
    private static partial Regex RegexRemoveHtml14();


    [GeneratedRegex(@"&#(\d+);", RegexOptions.IgnoreCase, "zh-CN")]
    private static partial Regex RegexRemoveHtml15();

    #endregion


    /// <summary>
    /// 过滤删除掉字符串中的 Emoji 表情
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static string RemoveEmoji(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // 允许：汉字、字母数字、空白、以及 . - _
        return RemoveEmojiRegex().Replace(value, "");
    }


    [GeneratedRegex(@"[^\u4E00-\u9FFFa-zA-Z0-9 ._-]+")]
    private static partial Regex RemoveEmojiRegex();


    /// <summary>
    /// 对文本进行指定长度截取并添加省略号
    /// </summary>
    /// <param name="NeiRong"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static string SubstringText(string text, int length)
    {
        if (text.Length > length)
        {
            text = text[..length];
            text += "...";
            return text;
        }
        else
        {
            return text;
        }
    }


    /// <summary>
    /// 对字符串进行局部隐藏处理
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static string PartiallyHidden(string text)
    {
        if (text.Length >= 3)
        {
            int group = text.Length / 3;

            string stars = text.Substring(group, group);

            string pstars = "";

            for (int i = 0; i < group; i++)
            {
                pstars += "*";
            }

            text = text.Replace(stars, pstars);
        }
        else
        {

            string stars = text.Substring(1, 1);

            string pstars = "";

            for (int i = 0; i < 1; i++)
            {
                pstars += "*";
            }

            text = text.Replace(stars, pstars);
        }

        return text;
    }


    /// <summary>
    /// Unicode转换中文
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static string UnicodeToString(string text)
    {
        return Regex.Unescape(text);
    }


    /// <summary>
    /// 过滤删除掉字符串中的 数字
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static string RemoveNumber(string key)
    {
        return RegexRemoveNumber().Replace(key, "");
    }

    [GeneratedRegex(@"\d")]
    private static partial Regex RegexRemoveNumber();


    /// <summary>
    /// 过滤删除掉字符串中的 非数字
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static string RemoveNotNumber(string key)
    {
        return RegexRemoveNotNumber().Replace(key, "");
    }

    [GeneratedRegex(@"[^\d]*")]
    private static partial Regex RegexRemoveNotNumber();


    /// <summary>
    /// 字符串压缩
    /// </summary>
    public static string CompressString(string text)
    {
        using MemoryStream memoryStream = new();
        using (GZipStream gZipStream = new(memoryStream, CompressionMode.Compress, true))
        {
            gZipStream.Write(Encoding.UTF8.GetBytes(text));
        }
        return Convert.ToBase64String(memoryStream.ToArray());
    }


    /// <summary>
    /// 字符串解压
    /// </summary>
    public static string DecompressString(string str)
    {
        byte[] compressBeforeByte = Convert.FromBase64String(str);

        using MemoryStream memoryStream = new(compressBeforeByte);
        using GZipStream gZipStream = new(memoryStream, CompressionMode.Decompress, true);

        using StreamReader streamReader = new(gZipStream, Encoding.UTF8);
        return streamReader.ReadToEnd();
    }


    /// <summary>
    /// 压缩GUID到22位的字符串
    /// </summary>
    /// <param name="value">待压缩的GUID</param>
    /// <returns></returns>
    public static string CompressGuid(Guid value)
    {

        string base64 = Convert.ToBase64String(value.ToByteArray());

        string encoded = base64.Replace("/", "_").Replace("+", "-");

        return encoded[..22];
    }


    /// <summary>
    /// 将22位的字符串还原为GUID
    /// </summary>
    /// <param name="vlaue">GUID压缩后的字符串</param>
    /// <returns></returns>
    public static Guid DecompressGuid(string vlaue)
    {
        Guid result = Guid.Empty;

        if ((!string.IsNullOrEmpty(vlaue)) && (vlaue.Trim().Length == 22))
        {
            string encoded = string.Concat(vlaue.Trim().Replace("-", "+").Replace("_", "/"), "==");

            try
            {
                byte[] base64 = Convert.FromBase64String(encoded);

                result = new(base64);
            }
            catch (FormatException)
            {
            }
        }

        return result;
    }


    /// <summary>
    /// Model对象转换为Uri网址参数形式
    /// </summary>
    /// <param name="obj">Model对象</param>
    /// <param name="url">前部分网址</param>
    /// <returns></returns>
    public static string ModelToUriParam(object obj, string url = "")
    {
        PropertyInfo[] properties = obj.GetType().GetProperties();
        StringBuilder sb = new();
        sb.Append(url);

        if (!url.Contains('?'))
        {
            sb.Append('?');
        }
        else if (!url.EndsWith('&') && !url.EndsWith('?'))
        {
            sb.Append('&');
        }

        foreach (var p in properties)
        {
            var value = p.GetValue(obj, null);
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                continue;
            }

            string stringValue;

            if (p.PropertyType.IsEnum || Nullable.GetUnderlyingType(p.PropertyType)?.IsEnum == true)
            {
                // 获取枚举的整数值
                stringValue = Convert.ToInt32(value).ToString();
            }
            else if (value is DateTimeOffset dateTimeOffset)
            {
                stringValue = dateTimeOffset.ToString("o", CultureInfo.InvariantCulture);
            }
            else if (value is DateTime dateTime)
            {
                stringValue = dateTime.ToString("o", CultureInfo.InvariantCulture);
            }
            else if (value is IFormattable formattable)
            {
                stringValue = formattable.ToString(null, CultureInfo.InvariantCulture);
            }
            else
            {
                stringValue = value.ToString()!;
            }

            sb.Append(HttpUtility.UrlEncode(p.Name));
            sb.Append('=');
            sb.Append(HttpUtility.UrlEncode(stringValue));
            sb.Append('&');
        }

        // 移除最后一个 '&' 字符
        if (sb[^1] == '&')
        {
            sb.Remove(sb.Length - 1, 1);
        }

        return sb.ToString();
    }


    /// <summary>
    /// 检查IPv4地址是否是局域网IP
    /// </summary>
    /// <param name="ipAddress"></param>
    /// <returns></returns>
    public static bool IsLanIpAddressV4(string ipAddress)
    {
        if (IPAddress.TryParse(ipAddress, out IPAddress? ip))
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                byte[] ipBytes = ip.GetAddressBytes();
                // A 类私有地址范围：10.0.0.0 - 10.255.255.255
                if (ipBytes[0] == 10)
                {
                    return true;
                }
                // B 类私有地址范围：172.16.0.0 - 172.31.255.255
                else if (ipBytes[0] == 172 && ipBytes[1] >= 16 && ipBytes[1] <= 31)
                {
                    return true;
                }
                // C 类私有地址范围：192.168.0.0 - 192.168.255.255
                else if (ipBytes[0] == 192 && ipBytes[1] == 168)
                {
                    return true;
                }
            }
        }
        return false;
    }


    /// <summary>
    /// 检查IPv6地址是否是局域网IP
    /// </summary>
    /// <param name="ipv6Address"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static bool IsLanIpAddressV6(string ipv6Address)
    {
        if (!IPAddress.TryParse(ipv6Address, out IPAddress? ipAddress))
        {
            //无效的IPv6地址
            return false;
        }

        //确保地址是IPv6  
        if (ipAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            //提供的IP地址不是IPv6地址
            return false;
        }

        //获取IPv6地址的字节数组  
        var bytes = ipAddress.GetAddressBytes();

        //检查是否是 ULA 地址
        if (bytes[0] >= 0xfc)
        {
            return true;
        }

        return false;
    }


    /// <summary>
    /// 检查IPv4地址是否在某个CIDR范围内
    /// </summary>
    /// <param name="ipAddress"></param>
    /// <param name="cidrRange"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static bool IsIpInCidrRangeV4(string ipAddress, string cidrRange)
    {
        string[] parts = cidrRange.Split('/');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!IPAddress.TryParse(parts[0], out IPAddress? network) ||
            !IPAddress.TryParse(ipAddress, out IPAddress? ipToCheck))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out int cidrMaskLength) || cidrMaskLength < 0 || cidrMaskLength > 32)
        {
            return false;
        }

        if (network.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
            ipToCheck.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        byte[] networkBytes = network.GetAddressBytes();
        byte[] ipBytes = ipToCheck.GetAddressBytes();

        byte[] maskBytes = new byte[4];
        for (int i = 0; i < cidrMaskLength / 8; i++)
        {
            maskBytes[i] = 0xFF;
        }
        if (cidrMaskLength % 8 != 0)
        {
            maskBytes[cidrMaskLength / 8] = (byte)(0xFF << 8 - cidrMaskLength % 8);
        }

        for (int i = 0; i < 4; i++)
        {
            if ((ipBytes[i] & maskBytes[i]) != (networkBytes[i] & maskBytes[i]))
            {
                return false;
            }
        }

        return true;
    }


    /// <summary>
    /// 检查IPv6地址是否在某个CIDR范围内
    /// </summary>
    /// <param name="ipAddress"></param>
    /// <param name="cidrNotation"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static bool IsIpInCidrRangeV6(string ipAddress, string cidrNotation)
    {
        // 解析IP地址  
        if (!IPAddress.TryParse(ipAddress, out IPAddress? ipToCheck))
        {
            //要检查的IP地址无效
            return false;
        }

        // 解析CIDR表示法  
        var cidrParts = cidrNotation.Split('/');
        if (cidrParts.Length != 2 || !int.TryParse(cidrParts[1], out int cidrBits))
        {
            //无效的CIDR表示法
            return false;
        }

        // 解析网络地址  
        if (!IPAddress.TryParse(cidrParts[0], out IPAddress? networkAddress))
        {
            //CIDR表示法中的网络地址无效
            return false;
        }

        // 确保地址都是IPv6  
        if (ipToCheck.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6 || networkAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            //两个IP地址都必须是IPv6
            return false;
        }

        // 获取IP地址和网络地址的字节数组  
        var ipBytes = ipToCheck.GetAddressBytes();
        var networkBytes = networkAddress.GetAddressBytes();


        var ipBigInteger = new BigInteger(ipBytes.Reverse().ToArray());
        var networkBigInteger = new BigInteger(networkBytes.Reverse().ToArray());

        var maskedNetwork = networkBigInteger >> (128 - cidrBits);

        return (ipBigInteger >> (128 - cidrBits)) == maskedNetwork;
    }


    /// <summary>
    /// 替换模板字符串中的参数
    /// </summary>
    /// <param name="template">模板字符串</param>
    /// <param name="placeholders">参数值字典集</param>
    /// <param name="placeholderPrefix">参数前缀，如 ${ </param>
    /// <param name="placeholderSuffix">参数后缀，如 } </param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static string ReplacePlaceholders(string template, Dictionary<string, string> placeholders, string placeholderPrefix, string placeholderSuffix)
    {
        string pattern = $"{Regex.Escape(placeholderPrefix)}(.*?){Regex.Escape(placeholderSuffix)}";

        return Regex.Replace(template, pattern, match =>
        {
            string key = match.Groups[1].Value;

            if (placeholders.TryGetValue(key, out string? value))
            {
                return value;
            }
            else
            {
                throw new Exception(key + "参数的值未找到");
            }
        });
    }


}
