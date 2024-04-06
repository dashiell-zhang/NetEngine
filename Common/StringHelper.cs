using System.IO.Compression;
using System.Net;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Common
{

    /// <summary>
    /// 针对string字符串常用操作方法
    /// </summary>
#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
    public class StringHelper
    {



        /// <summary>
        /// 过滤删除掉字符串中的 全部标点符号
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string RemovePunctuation(string text)
        {
            text = new string(text.Where(c => !char.IsPunctuation(c)).ToArray());

            return text;
        }



        /// <summary>
        /// 判断字符串中是否包含中文
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static bool IsContainsCN(string text)
        {
            Regex reg = new(@"[\u4e00-\u9fa5]");//正则表达式

            if (reg.IsMatch(text))
            {
                return true;
            }
            else
            {
                return false;
            }
        }



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
                htmlText = Regex.Replace(htmlText, @"<script[^>]*?>.*?</script>", "",

                RegexOptions.IgnoreCase);

                //删除HTML

                htmlText = Regex.Replace(htmlText, @"<(.[^>]*)>", "",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"([\r\n])[\s]+", "",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"-->", "", RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"<!--.*", "", RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"&(quot|#34);", "\"",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"&(amp|#38);", "&",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"&(lt|#60);", "<",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"&(gt|#62);", ">",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"&(nbsp|#160);", " ",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"&(iexcl|#161);", "\xa1",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"&(cent|#162);", "\xa2",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"&(pound|#163);", "\xa3",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"&(copy|#169);", "\xa9",

                RegexOptions.IgnoreCase);

                htmlText = Regex.Replace(htmlText, @"&#(\d+);", "",

                RegexOptions.IgnoreCase);

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



        /// <summary>
        /// 过滤删除掉字符串中的 Emoji 表情
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string RemoveEmoji(string value)
        {
            List<int> emojiCharNo = [12336, 65038, 8252, 8265, 42, 8419, 35, 12349, 169, 174, 8596, 8597, 8598, 8599, 8600, 8601, 8617, 8618, 9000, 9167, 9197, 9198, 9199, 9201, 9202, 9208, 9209, 9210, 9642, 9643, 9654, 9664, 9723, 9724, 9728, 9729, 9730, 9731, 9732, 9742, 9745, 9752, 9757, 9760, 9762, 9763, 9766, 9770, 9774, 9775, 9784, 9785, 9786, 9792, 9794, 9823, 9824, 9827, 9829, 9830, 9832, 9851, 9854, 9874, 9876, 9877, 9878, 9879, 9881, 9883, 9884, 9888, 9895, 9904, 9905, 9928, 9935, 9937, 9939, 9961, 9968, 9969, 9972, 9975, 9976, 9977, 9986, 9992, 9993, 9996, 9997, 9999, 10002, 10004, 10006, 10013, 10017, 10035, 10036, 10052, 10055, 10083, 10084, 10145, 10548, 10549, 11013, 11014, 11015, 55356, 57121, 57124, 57125, 57126, 57127, 57128, 57129, 57130, 57131, 57132, 57142, 57213, 57238, 57239, 57241, 57242, 57243, 57246, 57247, 57291, 57292, 57293, 57294, 57300, 57301, 57302, 57303, 57304, 57305, 57306, 57307, 57308, 57309, 57310, 57311, 57331, 57333, 57335, 55357, 56383, 56385, 56573, 56649, 56650, 56687, 56688, 56691, 56692, 56693, 56694, 56695, 56696, 56697, 56711, 56714, 56715, 56716, 56717, 56720, 56741, 56744, 56753, 56754, 56764, 56770, 56771, 56772, 56785, 56786, 56787, 56796, 56797, 56798, 56801, 56803, 56808, 56815, 56819, 56826, 57035, 57037, 57038, 57039, 57056, 57057, 57058, 57059, 57060, 57061, 57065, 57072, 57075, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 56689, 8505, 9410, 56702, 56703, 8482, 56834, 56887, 12951, 12953, 65039, 56832, 56835, 56836, 56833, 56838, 56837, 55358, 56611, 56898, 56899, 56841, 56842, 56839, 56845, 56617, 56856, 56855, 56858, 56857, 56690, 56843, 56859, 56860, 56618, 56861, 56593, 56599, 56621, 56619, 56596, 56592, 56616, 56848, 56849, 56886, 56847, 56850, 56900, 56876, 56613, 56844, 56852, 56874, 56612, 56884, 56594, 56597, 56610, 56622, 56615, 56885, 56623, 56608, 56846, 56595, 56784, 56853, 56863, 56897, 56878, 56879, 56882, 56883, 56698, 56870, 56871, 56872, 56880, 56869, 56866, 56877, 56881, 56854, 56867, 56862, 56851, 56873, 56875, 56868, 56865, 56864, 56620, 56840, 56447, 56448, 56489, 56609, 56441, 56442, 56443, 56445, 56446, 56598, 56890, 56888, 56889, 56891, 56892, 56893, 56896, 56895, 56894, 56904, 56905, 56906, 56459, 56460, 56472, 56477, 56470, 56471, 56467, 56478, 56469, 56479, 56468, 56475, 56474, 56473, 56476, 56590, 56740, 56589, 56495, 56482, 56485, 56491, 56486, 56488, 56483, 56492, 56493, 56484, 56395, 56602, 9995, 56726, 57073, 57074, 57076, 56396, 56588, 56591, 56606, 56607, 56600, 56601, 56392, 56393, 56390, 56725, 56391, 57077, 56397, 56398, 9994, 56394, 56603, 56604, 56399, 56908, 57078, 56400, 56626, 56605, 56911, 56453, 56627, 56490, 56766, 56767, 56757, 56758, 56386, 56763, 56387, 56800, 57024, 57025, 56759, 56756, 56384, 56389, 56388, 57062, 56438, 56422, 56423, 56433, 56424, 56788, 56425, 56436, 56437, 56909, 56910, 56901, 56902, 56449, 56907, 56783, 56903, 56614, 56631, 56430, 56450, 56439, 57029, 56628, 56440, 56435, 56434, 56789, 56629, 56432, 56624, 57027, 57028, 56625, 56444, 57221, 56630, 56760, 56761, 56793, 56794, 56795, 56799, 56780, 56454, 56455, 57014, 56781, 56782, 57283, 56451, 56431, 56790, 56791, 56634, 57287, 57282, 57284, 56995, 57290, 57012, 57013, 56632, 56636, 56637, 56638, 56633, 56792, 57036, 56429, 56427, 56428, 56463, 56465, 56426, 56420, 56421, 57026, 56419, 56752, 56755, 56373, 56338, 56743, 56374, 56341, 56750, 56361, 56378, 56733, 56369, 56328, 56705, 56367, 56325, 56326, 56372, 56334, 56708, 56723, 56748, 56366, 56322, 56323, 56324, 56375, 56342, 56343, 56381, 56335, 56337, 56336, 56362, 56363, 56729, 56722, 56344, 56739, 56719, 56731, 56365, 56321, 56320, 56377, 56368, 56327, 56747, 56724, 56379, 56360, 56380, 56742, 56728, 56737, 56382, 56707, 56340, 56339, 56355, 56356, 56357, 56358, 56359, 56709, 56710, 56738, 56713, 56745, 56730, 56732, 56376, 56330, 56354, 56718, 56333, 56370, 56329, 56371, 56331, 56364, 56749, 56351, 56352, 56353, 56712, 56345, 56346, 57016, 56332, 56347, 56348, 56349, 57010, 56350, 56727, 57011, 56706, 56735, 57008, 57009, 56736, 56464, 57144, 56494, 57015, 57145, 56640, 57146, 57147, 57148, 57143, 57137, 57138, 57139, 57140, 57141, 57150, 57151, 57152, 57153, 57154, 57155, 57017, 57018, 57159, 57160, 57161, 57162, 57163, 57164, 57165, 56685, 57166, 57167, 57168, 57169, 57170, 57171, 57040, 56669, 57157, 57042, 56677, 56657, 57158, 56660, 56661, 57149, 57041, 56658, 56684, 56678, 56773, 57156, 56668, 57048, 57136, 57182, 56656, 56662, 57043, 56680, 56670, 56775, 56768, 57174, 57175, 56681, 56659, 57172, 57183, 57173, 57133, 56682, 57134, 57135, 57044, 56665, 56774, 56666, 57203, 56664, 57202, 57045, 56675, 56663, 57215, 56776, 56683, 57201, 57176, 57177, 57178, 57179, 57180, 57181, 57184, 57186, 57187, 57188, 57189, 56686, 57185, 56671, 56672, 56673, 56704, 56734, 56721, 56746, 57190, 57191, 57192, 57193, 57194, 57218, 57200, 56769, 56679, 57195, 57196, 57197, 57198, 57199, 57212, 56667, 9749, 57046, 57205, 57206, 57214, 57207, 57208, 57209, 57210, 57211, 56642, 56643, 57047, 56676, 56779, 56777, 56778, 56674, 57204, 56644, 57049, 57338, 57101, 57102, 57103, 57104, 56830, 56813, 57099, 56827, 56817, 57000, 57312, 57313, 57314, 57315, 57316, 57317, 57318, 57320, 57321, 57322, 57323, 57324, 57325, 57327, 57328, 56466, 56828, 56829, 9962, 56652, 56653, 56651, 9970, 9978, 57089, 57091, 57092, 57093, 57094, 57095, 57097, 57248, 57053, 57249, 57250, 56456, 57258, 56962, 56963, 56964, 56965, 56966, 56967, 56968, 56969, 56970, 56989, 56990, 56971, 56972, 56973, 56974, 56976, 56977, 56978, 56979, 56980, 56981, 56982, 56983, 56984, 56985, 57083, 56986, 56987, 56988, 56765, 57082, 57081, 57084, 56975, 9981, 57054, 56997, 56998, 56999, 9875, 57055, 9973, 56996, 56994, 57067, 57068, 56506, 56961, 56991, 56992, 56993, 56960, 57080, 8987, 9203, 8986, 9200, 57105, 57106, 57107, 57108, 57109, 57110, 57111, 57112, 57113, 57114, 57115, 57116, 57117, 57118, 11088, 57119, 57120, 57100, 9925, 57088, 57096, 57090, 9748, 9889, 9924, 56487, 57098, 57219, 57220, 57222, 57223, 10024, 57224, 57225, 57226, 57227, 57229, 57230, 57231, 57232, 57233, 56807, 57216, 57217, 57259, 57286, 57285, 56647, 56648, 9917, 9918, 56654, 57280, 57296, 57288, 57289, 57278, 56655, 57267, 57295, 57297, 57298, 57299, 57336, 56645, 9971, 57251, 56639, 57277, 57279, 57079, 57263, 57265, 56831, 57004, 57262, 57264, 57266, 56809, 56824, 57001, 56527, 57268, 57261, 57256, 56821, 56822, 56403, 56701, 56700, 56762, 56404, 56405, 56406, 56804, 56805, 56806, 56407, 56408, 56699, 56945, 56946, 56947, 56409, 56410, 56411, 56412, 56413, 57234, 56948, 56414, 56415, 56416, 56417, 56944, 56418, 56401, 56402, 57257, 57235, 56802, 56575, 56452, 56461, 56462, 56583, 56584, 56585, 56586, 56546, 56547, 56559, 57276, 57269, 57270, 57252, 57255, 56571, 57271, 57272, 57273, 57274, 57275, 56641, 56561, 56562, 56542, 56543, 56544, 56587, 57003, 56507, 56509, 56510, 56511, 56512, 56814, 57253, 57260, 56570, 56567, 56568, 56569, 56572, 56481, 57326, 56532, 56533, 56534, 56535, 56536, 56537, 56538, 56531, 56530, 56515, 56540, 56516, 56560, 56529, 56496, 56500, 56501, 56502, 56503, 56504, 56499, 56505, 56551, 56552, 56553, 56548, 56549, 56550, 56555, 56554, 56556, 56557, 56558, 56541, 56508, 56513, 56514, 56517, 56518, 56519, 56520, 56521, 56522, 56523, 56524, 56525, 56526, 56528, 57337, 56751, 56816, 56818, 56810, 56811, 56812, 56545, 56457, 56952, 56458, 56953, 56956, 56954, 56955, 57002, 57021, 57023, 56820, 56823, 56825, 57063, 57319, 57006, 9855, 57019, 57020, 57022, 9940, 57005, 57007, 56565, 56579, 56580, 9800, 9801, 9802, 9803, 9804, 9805, 9806, 9807, 9808, 9809, 9810, 9811, 9934, 56576, 56577, 56578, 9193, 9194, 9195, 9196, 57254, 56581, 56582, 56566, 56563, 56564, 10133, 10134, 10135, 10067, 10068, 10069, 10071, 56497, 56498, 56539, 11093, 9989, 10060, 10062, 10160, 10175, 56912, 56913, 9899, 9898, 11035, 11036, 9726, 9725, 56635, 56480, 57281, 57228, 57332, 56128, 57339, 57340, 57341, 57342, 57343, 8205];

            foreach (var v in value)
            {
                if (emojiCharNo.Contains(v))
                {
                    value = value.Replace(v.ToString(), "");
                }
            }

            return value;
        }



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
            return Regex.Replace(key, @"\d", "");
        }



        /// <summary>
        /// 过滤删除掉字符串中的 非数字
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string RemoveNotNumber(string key)
        {
            return Regex.Replace(key, @"[^\d]*", "");
        }



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
            PropertyInfo[] propertis = obj.GetType().GetProperties();
            StringBuilder sb = new();
            sb.Append(url);
            sb.Append('?');
            foreach (var p in propertis)
            {
                var v = p.GetValue(obj, null);
                if (v == null)
                    continue;

                sb.Append(p.Name);
                sb.Append('=');
                sb.Append(HttpUtility.UrlEncode(v.ToString()));
                sb.Append('&');
            }
            sb.Remove(sb.Length - 1, 1);

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
}