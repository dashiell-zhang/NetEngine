using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Common;

/// <summary>
/// 提供XML序列化和反序列化能力
/// </summary>
public class XmlHelper
{


    /// <summary>
    /// 序列化对象
    /// </summary>
    /// <param name="obj">对象</param>
    /// <returns></returns>
    public static string ObjectToXml(object obj)
    {
        using MemoryStream memoryStream = new();
        XmlWriterSettings xmlWriterSettings = new()
        {
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = true
        };

        //去除默认命名空间xmlns:xsd和xmlns:xsi
        XmlSerializerNamespaces ns = new();
        ns.Add("", "");

        XmlSerializer xmlSerializer = new(obj.GetType());

        using (var xmlWriter = XmlWriter.Create(memoryStream, xmlWriterSettings))
        {
            xmlSerializer.Serialize(xmlWriter, obj, ns);
        }

        memoryStream.Position = 0;
        using StreamReader sr = new(memoryStream);
        var xmlString = sr.ReadToEnd();

        return xmlString;
    }




    /// <summary>
    /// 反序列化为对象
    /// </summary>
    /// <param name="xmlText">对象序列化后的Xml字符串</param>
    /// <returns></returns>
    public static T XmlToObject<T>(string xmlText)
    {
        using StringReader stringReader = new(xmlText);
        XmlReaderSettings xmlReaderSettings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        using XmlReader xmlReader = XmlReader.Create(stringReader, xmlReaderSettings);
        XmlSerializer xmlSerializer = new(typeof(T));
        object? result = xmlSerializer.Deserialize(xmlReader);

        if (result is T value)
        {
            return value;
        }

        throw new InvalidOperationException($"XML反序列化结果不能转换为 {typeof(T).FullName}");
    }


}
