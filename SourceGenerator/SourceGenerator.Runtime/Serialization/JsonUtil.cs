using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SourceGenerator.Runtime.Serialization;

/// <summary>
/// 为运行时提供统一的 JSON 序列化和反序列化工具
/// </summary>
public static class JsonUtil
{

    /// <summary>
    /// 参数键对象图允许的最大递归深度
    /// </summary>
    private const int MaxCanonicalDepth = 64;

    /// <summary>
    /// 单个参数集合允许参与键生成的最大元素数量
    /// </summary>
    private const int MaxCanonicalCollectionItems = 10000;


    /// <summary>
    /// 单个参数值允许生成的最大规范化 JSON 字节数
    /// </summary>
    private const int MaxCanonicalJsonBytes = 1024 * 1024;


    /// <summary>
    /// 缓存普通对象参与参数键的成员元数据
    /// </summary>
    private static readonly ConcurrentDictionary<Type, CanonicalMemberAccessor[]> CanonicalMembers = new();


    /// <summary>
    /// 表示一个可读取并参与参数键的成员
    /// </summary>
    private sealed class CanonicalMemberAccessor
    {

        /// <summary>
        /// 包含成员种类 声明类型和成员名称的稳定名称
        /// </summary>
        public required string Name { get; init; }


        /// <summary>
        /// 待读取的公开属性或字段
        /// </summary>
        public required MemberInfo Member { get; init; }


        /// <summary>
        /// 从目标对象读取当前成员值
        /// </summary>
        /// <param name="instance">目标对象</param>
        /// <returns>当前成员值</returns>
        public object? GetValue(object instance)
            => Member switch
            {
                PropertyInfo property => property.GetValue(instance),
                FieldInfo field => field.GetValue(instance),
                _ => throw new InvalidOperationException("参数键成员必须是属性或字段")
            };

    }


    /// <summary>
    /// 默认的 JSON 序列化配置
    /// </summary>
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReferenceHandler = ReferenceHandler.Preserve,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };


    /// <summary>
    /// 用于生成稳定参数键的严格 JSON 序列化配置
    /// </summary>
    private static readonly JsonSerializerOptions KeyJsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };


    /// <summary>
    /// 用于创建独立日志快照且不生成跨快照冲突的引用标识
    /// </summary>
    private static readonly JsonSerializerOptions SnapshotJsonOpts = new(JsonOpts)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };


    /// <summary>
    /// 将对象序列化为 JSON 字符串 在序列化和字符串转换失败时返回固定占位符
    /// </summary>
    /// <param name="value">待序列化的对象</param>
    /// <returns>JSON 字符串 回退字符串或失败占位符</returns>
    public static string ToJson(object? value)
    {
        try
        {
            return JsonSerializer.Serialize(value, JsonOpts);
        }
        catch
        {
            try
            {
                return value?.ToString() ?? "<null>";
            }
            catch
            {
                return "<serialization-failed>";
            }
        }
    }


    /// <summary>
    /// 创建可安全嵌入外层 JSON 的独立值快照 序列化失败时回退为字符串
    /// </summary>
    /// <param name="value">待创建快照的值</param>
    /// <returns>保留原始 JSON 类型的元素或回退字符串</returns>
    public static object? CreateSnapshotValue(object? value)
    {

        if (value is null)
            return null;

        try
        {
            return JsonSerializer.SerializeToElement(value, value.GetType(), SnapshotJsonOpts);
        }
        catch
        {
            try
            {
                return value.ToString() ?? "<null>";
            }
            catch
            {
                return "<serialization-failed>";
            }
        }

    }


    /// <summary>
    /// 尝试将对象序列化为属性顺序稳定的规范化 JSON
    /// </summary>
    /// <param name="value">待序列化的对象</param>
    /// <param name="json">成功时返回规范化 JSON</param>
    /// <returns>如果对象可以完整稳定序列化则返回 true</returns>
    public static bool TryToCanonicalJson(object? value, out string json)
    {

        try
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                var activeReferences = new HashSet<object>(ReferenceEqualityComparer.Instance);

                if (!TryWriteCanonicalValue(writer, value, activeReferences))
                {
                    json = string.Empty;
                    return false;
                }

                writer.Flush();
            }

            if (stream.Length > MaxCanonicalJsonBytes)
            {
                json = string.Empty;
                return false;
            }

            json = Encoding.UTF8.GetString(stream.ToArray());
            return true;
        }
        catch
        {
            json = string.Empty;
            return false;
        }

    }


    /// <summary>
    /// 将当前值连同实际运行时类型写入规范化参数键
    /// </summary>
    /// <param name="writer">目标 JSON 写入器</param>
    /// <param name="value">待写入的参数值</param>
    /// <param name="activeReferences">当前递归路径中的引用对象</param>
    /// <returns>如果值可以完整稳定写入则返回 true</returns>
    private static bool TryWriteCanonicalValue(Utf8JsonWriter writer, object? value, HashSet<object> activeReferences)
    {

        if (value is null)
        {
            writer.WriteNullValue();
            return true;
        }

        var runtimeType = value.GetType();

        if (IsUnsupportedCanonicalKeyValue(value, runtimeType))
            return false;

        var tracksReference = !runtimeType.IsValueType && value is not string;

        if (tracksReference && activeReferences.Count >= MaxCanonicalDepth)
            return false;

        if (tracksReference && !activeReferences.Add(value))
            return false;

        try
        {
            writer.WriteStartObject();
            writer.WriteString("$type", GetStableTypeName(runtimeType));
            writer.WritePropertyName("$value");

            if (value is Type representedType)
            {
                writer.WriteStringValue(GetStableTypeName(representedType));
            }
            else if (value is JsonDocument document)
            {
                WriteCanonicalJson(writer, document.RootElement);
            }
            else if (value is JsonElement element)
            {
                WriteCanonicalJson(writer, element);
            }
            else if (IsSimpleCanonicalValue(value, runtimeType))
            {
                if (!TryWriteSimpleCanonicalValue(writer, value, runtimeType))
                    return false;
            }
            else if (IsDictionaryType(runtimeType) && value is IEnumerable dictionary)
            {
                if (!TryWriteCanonicalDictionary(writer, dictionary, activeReferences))
                    return false;
            }
            else if (value is IEnumerable enumerable)
            {
                if (!IsSupportedCanonicalSequence(value, runtimeType)
                    || !TryWriteCanonicalSequence(writer, enumerable, activeReferences))
                    return false;
            }
            else if (!TryWriteCanonicalObject(writer, value, runtimeType, activeReferences))
            {
                return false;
            }

            writer.WriteEndObject();
            return true;
        }
        finally
        {
            if (tracksReference)
                activeReferences.Remove(value);
        }

    }


    /// <summary>
    /// 判断值是否属于可以直接通过 JSON 常量表示的稳定类型
    /// </summary>
    /// <param name="value">待检查的值</param>
    /// <param name="runtimeType">值的实际运行时类型</param>
    /// <returns>如果可以直接序列化则返回 true</returns>
    private static bool IsSimpleCanonicalValue(object value, Type runtimeType)
        => runtimeType.IsPrimitive
           || runtimeType.IsEnum
           || value is string
           || value is decimal
           || value is Half
           || value is Guid
           || value is DateTime
           || value is DateTimeOffset
           || value is TimeSpan
           || value is DateOnly
           || value is TimeOnly
           || value is Uri
           || value is Version
           || value is BigInteger;


    /// <summary>
    /// 将简单值按照数值等价和固定文化格式写入参数键
    /// </summary>
    /// <param name="writer">目标 JSON 写入器</param>
    /// <param name="value">待写入的简单值</param>
    /// <param name="runtimeType">值的实际运行时类型</param>
    /// <returns>如果简单值可以稳定写入则返回 true</returns>
    private static bool TryWriteSimpleCanonicalValue(Utf8JsonWriter writer, object value, Type runtimeType)
    {

        switch (value)
        {
            case string stringValue:
                if (Encoding.UTF8.GetByteCount(stringValue) > MaxCanonicalJsonBytes) return false;
                writer.WriteStringValue(stringValue);
                return true;
            case decimal decimalValue:
                writer.WriteRawValue(decimalValue.ToString("G29", CultureInfo.InvariantCulture));
                return true;
            case double doubleValue:
                if (!double.IsFinite(doubleValue)) return false;
                writer.WriteNumberValue(doubleValue == 0D ? 0D : doubleValue);
                return true;
            case float floatValue:
                if (!float.IsFinite(floatValue)) return false;
                writer.WriteNumberValue(floatValue == 0F ? 0F : floatValue);
                return true;
            case Half halfValue:
                if (!Half.IsFinite(halfValue)) return false;
                writer.WriteNumberValue(halfValue == (Half)0 ? 0F : (float)halfValue);
                return true;
            case BigInteger bigIntegerValue:
                writer.WriteStringValue(bigIntegerValue.ToString(CultureInfo.InvariantCulture));
                return true;
            default:
                var simpleElement = JsonSerializer.SerializeToElement(value, runtimeType, KeyJsonOpts);
                WriteCanonicalJson(writer, simpleElement);
                return true;
        }

    }


    /// <summary>
    /// 判断运行时值是否明确不适合参与稳定参数键
    /// </summary>
    /// <param name="value">待检查的值</param>
    /// <param name="runtimeType">值的实际运行时类型</param>
    /// <returns>如果无法安全生成稳定键则返回 true</returns>
    private static bool IsUnsupportedCanonicalKeyValue(object value, Type runtimeType)
        => value is Delegate
           || value is Stream
           || value is TextReader
           || value is TextWriter
           || value is CancellationTokenSource
           || typeof(Task).IsAssignableFrom(runtimeType)
           || ImplementsGenericInterface(runtimeType, typeof(IAsyncEnumerable<>));


    /// <summary>
    /// 按键和值的规范化内容排序后写入字典
    /// </summary>
    /// <param name="writer">目标 JSON 写入器</param>
    /// <param name="dictionary">待写入的字典枚举内容</param>
    /// <param name="activeReferences">当前递归路径中的引用对象</param>
    /// <returns>如果所有字典项均可稳定写入则返回 true</returns>
    private static bool TryWriteCanonicalDictionary(Utf8JsonWriter writer, IEnumerable dictionary, HashSet<object> activeReferences)
    {

        var entries = new List<(string Key, string Value)>();
        var totalEntryLength = 0L;

        foreach (var entry in dictionary)
        {
            if (entries.Count >= MaxCanonicalCollectionItems)
                return false;

            if (!TryGetDictionaryEntry(entry, out var key, out var value)
                || !TryCreateCanonicalJson(key, activeReferences, out var keyJson)
                || !TryCreateCanonicalJson(value, activeReferences, out var valueJson))
            {
                return false;
            }

            totalEntryLength += keyJson.Length + valueJson.Length;

            if (totalEntryLength > MaxCanonicalJsonBytes)
                return false;

            entries.Add((keyJson, valueJson));
        }

        entries.Sort(static (left, right) =>
        {
            var keyComparison = string.Compare(left.Key, right.Key, StringComparison.Ordinal);
            return keyComparison != 0
                ? keyComparison
                : string.Compare(left.Value, right.Value, StringComparison.Ordinal);
        });

        writer.WriteStartArray();

        foreach (var entry in entries)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("$key");
            writer.WriteRawValue(entry.Key);
            writer.WritePropertyName("$value");
            writer.WriteRawValue(entry.Value);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        return true;

    }


    /// <summary>
    /// 提取非泛型或泛型字典枚举项中的键和值
    /// </summary>
    /// <param name="entry">待解析的字典枚举项</param>
    /// <param name="key">解析得到的键</param>
    /// <param name="value">解析得到的值</param>
    /// <returns>如果枚举项包含可读取的键和值则返回 true</returns>
    private static bool TryGetDictionaryEntry(object? entry, out object? key, out object? value)
    {

        if (entry is DictionaryEntry dictionaryEntry)
        {
            key = dictionaryEntry.Key;
            value = dictionaryEntry.Value;
            return true;
        }

        if (entry is null)
        {
            key = null;
            value = null;
            return false;
        }

        var entryType = entry.GetType();
        var keyProperty = entryType.GetProperty("Key", BindingFlags.Instance | BindingFlags.Public);
        var valueProperty = entryType.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);

        if (keyProperty?.GetMethod is null || valueProperty?.GetMethod is null)
        {
            key = null;
            value = null;
            return false;
        }

        key = keyProperty.GetValue(entry);
        value = valueProperty.GetValue(entry);
        return true;

    }


    /// <summary>
    /// 按照现有元素顺序写入已物化序列
    /// </summary>
    /// <param name="writer">目标 JSON 写入器</param>
    /// <param name="enumerable">待写入的序列</param>
    /// <param name="activeReferences">当前递归路径中的引用对象</param>
    /// <returns>如果所有元素均可稳定写入则返回 true</returns>
    private static bool TryWriteCanonicalSequence(Utf8JsonWriter writer, IEnumerable enumerable, HashSet<object> activeReferences)
    {

        writer.WriteStartArray();
        var itemCount = 0;

        foreach (var item in enumerable)
        {
            if (itemCount >= MaxCanonicalCollectionItems)
                return false;

            if (!TryWriteCanonicalValue(writer, item, activeReferences))
                return false;

            if (writer.BytesCommitted + writer.BytesPending > MaxCanonicalJsonBytes)
                return false;

            itemCount++;
        }

        writer.WriteEndArray();
        return true;

    }


    /// <summary>
    /// 判断序列是否属于可以安全重复读取的常见已物化类型
    /// </summary>
    /// <param name="value">待检查的序列对象</param>
    /// <param name="runtimeType">序列的实际运行时类型</param>
    /// <returns>如果序列可以参与参数键生成则返回 true</returns>
    private static bool IsSupportedCanonicalSequence(object value, Type runtimeType)
        => runtimeType.IsArray || value is IList;


    /// <summary>
    /// 按公开可读属性和实例字段写入普通对象
    /// </summary>
    /// <param name="writer">目标 JSON 写入器</param>
    /// <param name="value">待写入的对象</param>
    /// <param name="runtimeType">对象的实际运行时类型</param>
    /// <param name="activeReferences">当前递归路径中的引用对象</param>
    /// <returns>如果对象状态可以完整稳定写入则返回 true</returns>
    private static bool TryWriteCanonicalObject(Utf8JsonWriter writer, object value, Type runtimeType, HashSet<object> activeReferences)
    {

        var members = CanonicalMembers.GetOrAdd(runtimeType, BuildCanonicalMembers);

        if (members.Length == 0)
            return false;

        writer.WriteStartObject();

        foreach (var member in members)
        {
            writer.WritePropertyName(member.Name);

            if (!TryWriteCanonicalValue(writer, member.GetValue(value), activeReferences))
                return false;

            if (writer.BytesCommitted + writer.BytesPending > MaxCanonicalJsonBytes)
                return false;
        }

        writer.WriteEndObject();
        return true;

    }


    /// <summary>
    /// 创建普通对象参与参数键的成员访问元数据
    /// </summary>
    /// <param name="runtimeType">对象的实际运行时类型</param>
    /// <returns>按照稳定名称排序后的成员访问器</returns>
    private static CanonicalMemberAccessor[] BuildCanonicalMembers(Type runtimeType)
    {

        var members = new List<CanonicalMemberAccessor>();

        foreach (var property in runtimeType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetMethod is null || !property.GetMethod.IsPublic || property.GetIndexParameters().Length != 0)
                continue;

            var declaringType = property.DeclaringType ?? runtimeType;
            members.Add(new CanonicalMemberAccessor
            {
                Name = "property|" + GetStableTypeName(declaringType) + "|" + property.Name,
                Member = property
            });
        }

        foreach (var field in runtimeType.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            var declaringType = field.DeclaringType ?? runtimeType;
            members.Add(new CanonicalMemberAccessor
            {
                Name = "field|" + GetStableTypeName(declaringType) + "|" + field.Name,
                Member = field
            });
        }

        return members.OrderBy(member => member.Name, StringComparer.Ordinal).ToArray();

    }


    /// <summary>
    /// 为嵌套值创建独立的规范化 JSON 片段
    /// </summary>
    /// <param name="value">待格式化的嵌套值</param>
    /// <param name="activeReferences">当前递归路径中的引用对象</param>
    /// <param name="json">生成的规范化 JSON</param>
    /// <returns>如果嵌套值可以完整稳定写入则返回 true</returns>
    private static bool TryCreateCanonicalJson(object? value, HashSet<object> activeReferences, out string json)
    {

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            if (!TryWriteCanonicalValue(writer, value, activeReferences))
            {
                json = string.Empty;
                return false;
            }

            writer.Flush();
        }

        if (stream.Length > MaxCanonicalJsonBytes)
        {
            json = string.Empty;
            return false;
        }

        json = Encoding.UTF8.GetString(stream.ToArray());
        return true;

    }


    /// <summary>
    /// 判断类型是否属于字典契约
    /// </summary>
    /// <param name="type">待检查的运行时类型</param>
    /// <returns>如果类型实现字典接口则返回 true</returns>
    private static bool IsDictionaryType(Type type)
        => typeof(IDictionary).IsAssignableFrom(type)
           || ImplementsGenericInterface(type, typeof(IDictionary<,>))
           || ImplementsGenericInterface(type, typeof(IReadOnlyDictionary<,>));


    /// <summary>
    /// 判断类型自身或其接口是否匹配指定开放泛型接口
    /// </summary>
    /// <param name="type">待检查的运行时类型</param>
    /// <param name="genericInterface">开放泛型接口类型</param>
    /// <returns>如果类型实现指定接口则返回 true</returns>
    private static bool ImplementsGenericInterface(Type type, Type genericInterface)
    {

        if (type.IsGenericType && type.GetGenericTypeDefinition() == genericInterface)
            return true;

        return type.GetInterfaces().Any(interfaceType => interfaceType.IsGenericType
            && interfaceType.GetGenericTypeDefinition() == genericInterface);

    }


    /// <summary>
    /// 获取不包含程序集版本信息的稳定运行时类型名称
    /// </summary>
    /// <param name="type">待格式化的运行时类型</param>
    /// <returns>包含程序集简单名称的稳定类型名称</returns>
    private static string GetStableTypeName(Type type)
    {

        if (type.IsArray)
        {
            var elementType = type.GetElementType();
            return elementType is null
                ? type.Name
                : GetStableTypeName(elementType) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var arguments = string.Join(",", type.GetGenericArguments().Select(GetStableTypeName));
            return definition.Assembly.GetName().Name
                + "|"
                + (definition.FullName ?? definition.Name)
                + "["
                + arguments
                + "]";
        }

        return type.Assembly.GetName().Name + "|" + (type.FullName ?? type.Name);

    }


    /// <summary>
    /// 按照固定属性顺序写入 JSON 元素
    /// </summary>
    /// <param name="writer">JSON 写入器</param>
    /// <param name="element">待写入的 JSON 元素</param>
    /// <param name="depth">当前递归深度</param>
    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement element, int depth = 0)
    {

        if (depth > MaxCanonicalDepth)
            throw new JsonException("参数 JSON 超过允许的最大递归深度");

        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            var propertyCount = 0;

            foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                if (propertyCount >= MaxCanonicalCollectionItems)
                    throw new JsonException("参数 JSON 对象属性数量超过允许上限");

                writer.WritePropertyName(property.Name);
                WriteCanonicalJson(writer, property.Value, depth + 1);
                propertyCount++;
            }

            writer.WriteEndObject();
            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            var itemCount = 0;

            foreach (var item in element.EnumerateArray())
            {
                if (itemCount >= MaxCanonicalCollectionItems)
                    throw new JsonException("参数 JSON 数组元素数量超过允许上限");

                WriteCanonicalJson(writer, item, depth + 1);
                itemCount++;
            }

            writer.WriteEndArray();
            return;
        }

        element.WriteTo(writer);

    }
}
