using Common;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace WebAPI.Core.Libraries.Swagger;

/// <summary>
/// 提供枚举相关的 Swagger 文档辅助方法
/// </summary>
/// <remarks>
/// 该类主要用于从 XML 文档注释中读取枚举成员的 summary
/// 并将其拼接成对前端友好的枚举映射文本
/// </remarks>
internal static class EnumDocHelper
{

    /// <summary>
    /// XML 文档成员名到 summary 的缓存
    /// </summary>
    /// <remarks>
    /// key 示例为 F:Namespace.Type.Member
    /// </remarks>
    private static readonly Lazy<ConcurrentDictionary<string, string>> XmlSummaryByMemberName = new(LoadXmlSummaryByMemberName, true);


    /// <summary>
    /// 获取枚举成员的描述文本
    /// </summary>
    /// <param name="enumType">枚举类型</param>
    /// <param name="memberName">枚举成员名称</param>
    /// <returns>优先返回 XML summary 其次返回 DescriptionAttribute 最后返回 null</returns>
    public static string? GetEnumMemberDescription(Type enumType, string memberName)
        => GetEnumMemberSummary(enumType, memberName)
           ?? enumType.GetField(memberName, BindingFlags.Public | BindingFlags.Static)?.GetCustomAttribute<DescriptionAttribute>()?.Description;


    /// <summary>
    /// 构建枚举值到含义的映射文本
    /// </summary>
    /// <param name="enumType">枚举类型</param>
    /// <returns>示例 0 = None: 无; 1 = Read</returns>
    /// <remarks>
    /// 当某个枚举成员没有注释时 仅输出 1 = Read 以避免 Read: Read 的重复展示
    /// </remarks>
    public static string BuildEnumMappingText(Type enumType)
    {
        var sb = new StringBuilder(256);

        // Enum.GetValues 返回的顺序与 Swashbuckle 输出到 schema.enum 的顺序保持一致
        foreach (var value in Enum.GetValues(enumType).Cast<object>())
        {
            var longValue = Convert.ToInt64(value);
            var name = Enum.GetName(enumType, value);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var description = GetEnumMemberDescription(enumType, name);

            if (sb.Length > 0)
            {
                sb.Append("; ");
            }

            sb.Append(longValue);
            sb.Append(" = ");
            sb.Append(name);

            // 仅当描述存在且不等于名称时才追加描述
            if (!string.IsNullOrWhiteSpace(description) && !string.Equals(description, name, StringComparison.Ordinal))
            {
                sb.Append(": ");
                sb.Append(description);
            }
        }

        return sb.ToString();
    }


    /// <summary>
    /// 从已加载的 XML 文档中获取枚举成员的 summary
    /// </summary>
    /// <param name="enumType">枚举类型</param>
    /// <param name="memberName">枚举成员名称</param>
    /// <returns>summary 文本 不存在则返回 null</returns>
    private static string? GetEnumMemberSummary(Type enumType, string memberName)
    {
        var fullName = enumType.FullName;
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return null;
        }

        var fullNameWithPlus = fullName;
        var fullNameWithDot = fullName.Replace('+', '.');

        var key1 = "F:" + fullNameWithPlus + "." + memberName;
        if (XmlSummaryByMemberName.Value.TryGetValue(key1, out var summary1))
        {
            return summary1;
        }

        var key2 = "F:" + fullNameWithDot + "." + memberName;
        if (XmlSummaryByMemberName.Value.TryGetValue(key2, out var summary2))
        {
            return summary2;
        }

        return null;
    }


    /// <summary>
    /// 从应用输出目录扫描并加载所有 XML 文档文件
    /// </summary>
    /// <returns>成员名到 summary 的映射</returns>
    private static ConcurrentDictionary<string, string> LoadXmlSummaryByMemberName()
    {
        var map = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        try
        {
            // AppContext.BaseDirectory 通常为应用的 bin 输出目录
            // IncludeXmlComments 已把这些 XML 文件用于 Swagger 生成 这里复用同样的文件集合
            var xmlPaths = IOHelper.GetFolderAllFiles(AppContext.BaseDirectory)
                .Where(p => p.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var xmlPath in xmlPaths)
            {
                TryLoadXmlFile(xmlPath, map);
            }
        }
        catch
        {
            // 忽略 XML 文档加载失败，Swagger 仍可正常生成
        }

        return map;
    }


    /// <summary>
    /// 尝试加载单个 XML 文档文件并合并到缓存
    /// </summary>
    /// <param name="xmlPath">XML 文件路径</param>
    /// <param name="map">输出缓存</param>
    private static void TryLoadXmlFile(string xmlPath, ConcurrentDictionary<string, string> map)
    {
        try
        {
            var doc = XDocument.Load(xmlPath);
            var members = doc.Root?.Element("members")?.Elements("member");
            if (members is null)
            {
                return;
            }

            foreach (var member in members)
            {
                var name = member.Attribute("name")?.Value;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var summary = member.Element("summary")?.Value;
                summary = NormalizeSummary(summary);
                if (string.IsNullOrWhiteSpace(summary))
                {
                    continue;
                }

                map.TryAdd(name, summary);
            }
        }
        catch
        {
            // ignore single file
        }
    }


    /// <summary>
    /// 规范化 XML summary 文本
    /// </summary>
    /// <param name="summary">原始 summary</param>
    /// <returns>去除多余空白后的 summary</returns>
    /// <remarks>
    /// XML 文档的 summary 往往包含换行与缩进 这里统一压缩为空格并 trim
    /// </remarks>
    private static string? NormalizeSummary(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return null;
        }

        var sb = new StringBuilder(summary.Length);
        var prevIsWhitespace = false;
        foreach (var ch in summary)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!prevIsWhitespace)
                {
                    sb.Append(' ');
                    prevIsWhitespace = true;
                }
                continue;
            }

            sb.Append(ch);
            prevIsWhitespace = false;
        }

        return sb.ToString().Trim();
    }

}
