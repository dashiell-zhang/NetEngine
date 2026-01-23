using System.Reflection;
using System.Text;
using System.Globalization;
using System.Web;

namespace Admin.App.Libraries
{
    public class StringHelper
    {

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
    }
}
