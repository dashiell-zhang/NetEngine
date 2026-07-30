using System.Collections;
using System.Reflection;

namespace Common;

/// <summary>
/// 提供对象属性读取、比较、赋值和克隆能力
/// </summary>
public class PropertyHelper
{

    [ThreadStatic]
    private static Dictionary<object, Dictionary<Type, object>>? cloneMap;


    [ThreadStatic]
    private static int cloneDepth;

    /// <summary>  
    /// 反射得到实体类的字段名称和值  
    /// </summary>  
    /// <typeparam name="T">实体类</typeparam>  
    /// <param name="t">实例化</param>  
    /// <returns></returns>  
    public static Dictionary<object, object?> GetProperties<T>(T t) where T : notnull, new()
    {
        Dictionary<object, object?> ret = [];

        PropertyInfo[] properties = t.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(t => t.CanRead && t.GetIndexParameters().Length == 0).ToArray();

        foreach (PropertyInfo item in properties)
        {

            string name = item.Name;
            object? value = item.GetValue(t, null);

            if (item.PropertyType == typeof(DateTime))
            {
                ret.Add(name, Convert.ToDateTime(value).ToString("yyyy/MM/dd HH:mm:ss.fff"));
            }
            else if (item.PropertyType.IsValueType || item.PropertyType.Name.StartsWith("String"))
            {
                ret.Add(name, value);
            }
        }

        return ret;
    }



    /// <summary>  
    /// 反射得到实体类的字段显示名称和值  
    /// </summary>  
    /// <typeparam name="T">实体类</typeparam>  
    /// <param name="t">实例化</param>  
    /// <returns></returns>  
    public static Dictionary<object, object?> GetPropertiesDisplayName<T>(T t) where T : notnull
    {
        Dictionary<object, object?> ret = [];

        PropertyInfo[] properties = t.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(t => t.CanRead && t.GetIndexParameters().Length == 0).ToArray();

        foreach (PropertyInfo item in properties)
        {
            object? displayName = item.CustomAttributes.Where(t => t.AttributeType.Name == "DisplayNameAttribute").Select(t => t.ConstructorArguments.Select(v => v.Value).FirstOrDefault()).FirstOrDefault();

            string name = displayName != null ? displayName.ToString()! : item.Name;
            object? value = item.GetValue(t, null);

            if (item.PropertyType == typeof(DateTime))
            {
                ret.Add(name, Convert.ToDateTime(value).ToString("yyyy/MM/dd HH:mm:ss.fff"));
            }
            else if (item.PropertyType.IsValueType || item.PropertyType.Name.StartsWith("String"))
            {
                ret.Add(name, value);
            }
        }
        return ret;
    }



    /// <summary>
    /// 比较两个实体的值输出差异结果
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="original">原始值</param>
    /// <param name="after">修改后的值</param>
    /// <returns></returns>
    public static string ComparisonEntity<T>(T original, T after) where T : new()
    {
        var retValue = "";

        var fields = typeof(T).GetProperties().Where(t => t.CanRead && t.GetIndexParameters().Length == 0).ToArray();

        for (int i = 0; i < fields.Length; i++)
        {
            var pi = fields[i];

            object? oldObject = pi.GetValue(original);
            object? newObject = pi.GetValue(after);
            Type propertyType = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType;

            if (!IsValueEquals(oldObject, newObject, propertyType))
            {

                retValue += pi.Name + ":";

                if (propertyType == typeof(bool))
                {
                    retValue += FormatBool(oldObject) + " -> ";
                    retValue += FormatBool(newObject) + "； \n";
                }
                else if (propertyType == typeof(DateTime))
                {
                    retValue += (oldObject is DateTime oldDateTime ? oldDateTime.ToString("yyyy-MM-dd") : "") + " ->";
                    retValue += (newObject is DateTime newDateTime ? newDateTime.ToString("yyyy-MM-dd") : "") + "； \n";
                }
                else
                {
                    retValue += (oldObject?.ToString() ?? "") + " -> ";
                    retValue += (newObject?.ToString() ?? "") + "； \n";
                }
            }
        }

        return retValue;

        static bool IsValueEquals(object? oldObject, object? newObject, Type propertyType)
        {
            if (oldObject == null || newObject == null)
            {
                return oldObject == null && newObject == null;
            }

            if (propertyType == typeof(decimal))
            {
                return Convert.ToDecimal(oldObject) == Convert.ToDecimal(newObject);
            }

            return oldObject.Equals(newObject);
        }

        static string FormatBool(object? value)
        {
            return value is bool boolValue ? (boolValue ? "是" : "否") : "";
        }
    }



    /// <summary>
    /// 给对象赋值的方法(同一个类型)
    /// </summary>
    /// <param name="left">=号左边</param>
    /// <param name="right">=号右边</param>
    public static void Assignment<T>(T left, T right) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (ReferenceEquals(left, right))
        {
            return;
        }

        bool isRootClone = cloneDepth == 0;

        if (isRootClone)
        {
            cloneMap = new(ReferenceEqualityComparer.Instance);
        }

        cloneDepth++;

        try
        {
            RegisterClone(right, left.GetType(), left);
            Type type = left.GetType();

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>)) // 检查是否为 Dictionary 类型
            {

                if (left is not IDictionary leftDict || right is not IDictionary rightDict)
                {
                    throw new ArgumentException("左右对象的实际类型必须都是 Dictionary");
                }

                var keyType = type.GetGenericArguments()[0];
                var valueType = type.GetGenericArguments()[1];
                List<DictionaryEntry> clonedEntries = [];

                foreach (DictionaryEntry entry in rightDict)
                {
                    var clonedKey = Clone(entry.Key, keyType);
                    var clonedValue = Clone(entry.Value!, valueType);
                    clonedEntries.Add(new DictionaryEntry(clonedKey!, clonedValue));
                }

                ReplaceDictionaryContents(leftDict, clonedEntries);

            }
            else if (!type.IsArray && typeof(IList).IsAssignableFrom(type))  // 检查是否为集合类型
            {
                if (left is not IList leftList || right is not IList rightList)
                {
                    throw new ArgumentException("左右对象的实际类型必须都是集合");
                }

                var rightEnumerator = rightList.GetEnumerator();
                var elementType = GetListElementType(rightList.GetType());
                List<object?> clonedItems = [];

                while (rightEnumerator.MoveNext())
                {
                    var clonedValue = Clone(rightEnumerator.Current, elementType);

                    clonedItems.Add(clonedValue);
                }

                ReplaceListContents(leftList, clonedItems);

            }
            else
            {
                var properties = type.GetProperties().Where(t => t.CanRead && t.CanWrite && t.GetIndexParameters().Length == 0);

                foreach (var prop in properties)
                {
                    var value = prop.GetValue(right);

                    var clonedValue = Clone(value, prop.PropertyType);

                    prop.SetValue(left, clonedValue);
                }
            }
        }
        finally
        {
            cloneDepth--;

            if (isRootClone)
            {
                cloneMap = null;
            }
        }

        static object? Clone(object? original, Type type)
        {
            if (original == null) return null;

            if (TryGetClone(original, type, out var existingClone))
            {
                return existingClone;
            }

            Type runtimeType = original.GetType();

            if (type.IsAssignableFrom(runtimeType))
            {
                type = runtimeType;
            }

            if (TryGetClone(original, type, out existingClone))
            {
                return existingClone;
            }

            if (type.IsValueType || type == typeof(string) || type == typeof(object))
            {
                return original;
            }
            else if (type.IsArray)
            {
                var sourceArray = (Array)original;
                var elementType = type.GetElementType() ?? typeof(object);
                int[] lengths = Enumerable.Range(0, sourceArray.Rank).Select(sourceArray.GetLength).ToArray();
                int[] lowerBounds = Enumerable.Range(0, sourceArray.Rank).Select(sourceArray.GetLowerBound).ToArray();
                var clonedArray = type.IsSZArray ? Array.CreateInstance(elementType, lengths[0]) : Array.CreateInstance(elementType, lengths, lowerBounds);
                RegisterClone(original, type, clonedArray);

                foreach (var indices in EnumerateArrayIndices(sourceArray))
                {
                    clonedArray.SetValue(Clone(sourceArray.GetValue(indices), elementType), indices);
                }

                return clonedArray;
            }
            else if (typeof(IList).IsAssignableFrom(type))
            {
                if (type.IsInterface || type.IsAbstract || type.GetConstructor(Type.EmptyTypes) == null || Activator.CreateInstance(type) is not IList clonedList || original is not IList sourceList)
                {
                    return original;
                }

                var elementType = GetListElementType(type);
                RegisterClone(original, type, clonedList);

                foreach (var item in sourceList)
                {
                    clonedList.Add(Clone(item, elementType));
                }

                return clonedList;
            }
            else if (type.IsInterface || type.IsAbstract || type.GetConstructor(Type.EmptyTypes) == null)
            {
                return original;
            }
            else
            {
                var cloneMethod = typeof(PropertyHelper).GetMethod("Assignment")!.MakeGenericMethod(type);
                var clonedObject = Activator.CreateInstance(type);
                RegisterClone(original, type, clonedObject!);
                cloneMethod.Invoke(null, [clonedObject, original]);
                return clonedObject!;
            }
        }
    }



    /// <summary>
    /// 克隆对象
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="obj"></param>
    public static T? Clone<T>(T obj) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(obj);

        Type type = obj.GetType();

        if (type.GetConstructor(Type.EmptyTypes) == null)
        {
            throw new NotSupportedException($"类型 {type.FullName} 必须具有公共无参构造函数");
        }

        var clonedObject = Activator.CreateInstance(type);

        var cloneMethod = typeof(PropertyHelper).GetMethod("Assignment")!.MakeGenericMethod(type);

        cloneMethod.Invoke(null, [clonedObject, obj]);

        return (T?)clonedObject;
    }



    /// <summary>
    /// 给对象赋值的方法(不同类型)
    /// </summary>
    /// <param name="left">=号左边</param>
    /// <param name="right">=号右边</param>
    public static void AssignmentDifferentType<L, R>(L left, R right) where L : class, new() where R : class, new()
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (ReferenceEquals(left, right))
        {
            return;
        }

        bool isRootClone = cloneDepth == 0;

        if (isRootClone)
        {
            cloneMap = new(ReferenceEqualityComparer.Instance);
        }

        cloneDepth++;

        try
        {
            RegisterClone(right, left.GetType(), left);
            Type ltype = left.GetType();
            Type rtype = right.GetType();

            if (ltype.IsGenericType && ltype.GetGenericTypeDefinition() == typeof(Dictionary<,>)) // 检查是否为 Dictionary 类型
            {
                if (rtype.IsGenericType && rtype.GetGenericTypeDefinition() == typeof(Dictionary<,>)) // 检查是否为 Dictionary 类型
                {
                    if (left is IDictionary leftDict && right is IDictionary rightDict)
                    {
                        var lKeyType = ltype.GetGenericArguments()[0];
                        var rKeyType = rtype.GetGenericArguments()[0];

                        if (lKeyType == rKeyType)
                        {
                            var lValueType = ltype.GetGenericArguments()[1];
                            var rValueType = rtype.GetGenericArguments()[1];
                            List<DictionaryEntry> clonedEntries = [];

                            foreach (DictionaryEntry entry in rightDict)
                            {
                                var clonedKey = Clone(entry.Key, lKeyType);

                                if (entry.Value == null)
                                {
                                    if (IsNullable(lValueType))
                                    {
                                        clonedEntries.Add(new DictionaryEntry(clonedKey!, null));
                                    }
                                    else
                                    {
                                        throw new ArgumentException($"字典值不能从 null 赋值给 {lValueType.FullName}");
                                    }
                                }
                                else
                                {
                                    var clonedValue = Clone(entry.Value, lValueType);

                                    if (clonedValue == null)
                                    {
                                        throw new ArgumentException($"字典值不能从 {rValueType.FullName} 赋值给 {lValueType.FullName}");
                                    }

                                    clonedEntries.Add(new DictionaryEntry(clonedKey!, clonedValue));
                                }

                            }

                            ReplaceDictionaryContents(leftDict, clonedEntries);
                        }
                        else
                        {
                            throw new Exception("左右都必须是 Dictionary 的Key必须是同一个类型");
                        }
                    }
                }
                else
                {
                    throw new Exception("左右都必须是 Dictionary 类型");
                }
            }
            else if (!ltype.IsArray && typeof(IList).IsAssignableFrom(ltype))  // 检查是否为集合类型
            {

                if (typeof(IList).IsAssignableFrom(rtype))
                {
                    if (left is IList leftList && right is IList rightList)
                    {
                        var lType = GetListElementType(leftList.GetType());
                        var rType = GetListElementType(rightList.GetType());
                        var rightEnumerator = rightList.GetEnumerator();
                        List<object?> clonedItems = [];

                        while (rightEnumerator.MoveNext())
                        {
                            if (rightEnumerator.Current == null)
                            {
                                if (!IsNullable(lType))
                                {
                                    throw new ArgumentException($"集合元素不能从 null 赋值给 {lType.FullName}");
                                }

                                clonedItems.Add(null);
                            }
                            else
                            {
                                var clonedValue = Clone(rightEnumerator.Current, lType);

                                if (clonedValue == null)
                                {
                                    throw new ArgumentException($"集合元素不能从 {rType.FullName} 赋值给 {lType.FullName}");
                                }

                                clonedItems.Add(clonedValue);
                            }
                        }

                        ReplaceListContents(leftList, clonedItems);
                    }
                }
                else
                {
                    throw new Exception("左右都必须是 集合 类型");
                }
            }
            else
            {
                var lProperties = ltype.GetProperties().Where(prop => prop.CanWrite && prop.GetIndexParameters().Length == 0);
                var rProperties = rtype.GetProperties().Where(prop => prop.CanRead && prop.GetIndexParameters().Length == 0);

                foreach (var lProp in lProperties)
                {
                    var rProp = rProperties.FirstOrDefault(p => p.Name == lProp.Name);

                    if (rProp != null)
                    {
                        object? rValue = rProp.GetValue(right);

                        Type lType = lProp.PropertyType;

                        if (rValue != null)
                        {
                            var clonedValue = Clone(rValue, lType);

                            if (clonedValue != null)
                            {
                                lProp.SetValue(left, clonedValue);
                            }
                        }
                        else
                        {
                            if (IsNullable(lType))
                            {
                                lProp.SetValue(left, null);
                            }

                        }
                    }
                }
            }
        }
        finally
        {
            cloneDepth--;

            if (isRootClone)
            {
                cloneMap = null;
            }
        }


        static bool IsNullable(Type type)
        {
            bool isNullable = false;

            if (Nullable.GetUnderlyingType(type) != null)
            {
                isNullable = true;
            }
            else if (!type.IsValueType)
            {
                isNullable = true;
            }

            return isNullable;
        }

        static object? Clone(object original, Type targetType)
        {
            if (TryGetClone(original, targetType, out var existingClone))
            {
                return existingClone;
            }

            Type sourceType = original.GetType();
            Type effectiveTargetType = targetType;

            if (!targetType.IsValueType && targetType.IsAssignableFrom(sourceType))
            {
                effectiveTargetType = sourceType;
            }

            if (TryGetClone(original, effectiveTargetType, out existingClone))
            {
                return existingClone;
            }

            if (effectiveTargetType.IsValueType || effectiveTargetType == typeof(string) || sourceType.IsValueType || sourceType == typeof(string))
            {
                return effectiveTargetType.IsAssignableFrom(sourceType) ? original : null;
            }

            if (effectiveTargetType.IsArray && sourceType.IsArray && original is Array sourceArray)
            {
                if (effectiveTargetType.GetArrayRank() != sourceType.GetArrayRank() || sourceArray.Rank != sourceType.GetArrayRank())
                {
                    throw new ArgumentException($"数组维度不一致，无法从 {sourceType.FullName} 赋值给 {effectiveTargetType.FullName}");
                }

                var targetElementType = effectiveTargetType.GetElementType() ?? typeof(object);
                int[] lengths = Enumerable.Range(0, sourceArray.Rank).Select(sourceArray.GetLength).ToArray();
                int[] lowerBounds = Enumerable.Range(0, sourceArray.Rank).Select(sourceArray.GetLowerBound).ToArray();
                var clonedArray = effectiveTargetType.IsSZArray ? Array.CreateInstance(targetElementType, lengths[0]) : Array.CreateInstance(targetElementType, lengths, lowerBounds);
                RegisterClone(original, effectiveTargetType, clonedArray);

                foreach (var indices in EnumerateArrayIndices(sourceArray))
                {
                    var item = sourceArray.GetValue(indices);
                    object? clonedItem;

                    if (item == null)
                    {
                        if (!IsNullable(targetElementType))
                        {
                            throw new ArgumentException($"数组元素不能从 null 赋值给 {targetElementType.FullName}");
                        }

                        clonedItem = null;
                    }
                    else
                    {
                        clonedItem = Clone(item, targetElementType);

                        if (clonedItem == null)
                        {
                            throw new ArgumentException($"数组元素不能从 {item.GetType().FullName} 赋值给 {targetElementType.FullName}");
                        }
                    }

                    clonedArray.SetValue(clonedItem, indices);
                }

                return clonedArray;
            }

            if (typeof(IList).IsAssignableFrom(effectiveTargetType) && typeof(IList).IsAssignableFrom(sourceType))
            {
                if (effectiveTargetType.IsInterface || effectiveTargetType.IsAbstract || effectiveTargetType.GetConstructor(Type.EmptyTypes) == null || Activator.CreateInstance(effectiveTargetType) is not IList clonedList || original is not IList sourceList)
                {
                    return effectiveTargetType.IsAssignableFrom(sourceType) ? original : null;
                }

                var targetElementType = GetListElementType(effectiveTargetType);
                RegisterClone(original, effectiveTargetType, clonedList);

                foreach (var item in sourceList)
                {
                    if (item == null)
                    {
                        if (!IsNullable(targetElementType))
                        {
                            throw new ArgumentException($"集合元素不能从 null 赋值给 {targetElementType.FullName}");
                        }

                        clonedList.Add(null);
                    }
                    else
                    {
                        var clonedItem = Clone(item, targetElementType);

                        if (clonedItem == null)
                        {
                            throw new ArgumentException($"集合元素不能从 {item.GetType().FullName} 赋值给 {targetElementType.FullName}");
                        }

                        clonedList.Add(clonedItem);
                    }
                }

                return clonedList;
            }

            if (effectiveTargetType.IsInterface || effectiveTargetType.IsAbstract || effectiveTargetType.GetConstructor(Type.EmptyTypes) == null)
            {
                return effectiveTargetType.IsAssignableFrom(sourceType) ? original : null;
            }

            if (effectiveTargetType == sourceType)
            {

                var cloneMethod = typeof(PropertyHelper).GetMethod("Assignment")!.MakeGenericMethod(effectiveTargetType);
                var clonedObject = Activator.CreateInstance(effectiveTargetType);
                RegisterClone(original, effectiveTargetType, clonedObject!);
                cloneMethod.Invoke(null, [clonedObject, original]);
                return clonedObject;
            }
            else
            {
                var cloneMethod = typeof(PropertyHelper).GetMethod("AssignmentDifferentType")!.MakeGenericMethod(effectiveTargetType, sourceType);
                var clonedObject = Activator.CreateInstance(effectiveTargetType);
                RegisterClone(original, effectiveTargetType, clonedObject!);
                cloneMethod.Invoke(null, [clonedObject, original]);
                return clonedObject;
            }
        }
    }


    /// <summary>
    /// 获取源对象指定目标类型或兼容类型的克隆实例
    /// </summary>
    /// <param name="source">源对象</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="clone">克隆实例</param>
    /// <returns>是否存在对应克隆实例</returns>
    private static bool TryGetClone(object source, Type targetType, out object clone)
    {
        if (cloneMap != null && cloneMap.TryGetValue(source, out var targetClones) && targetClones.TryGetValue(targetType, out var existingClone))
        {
            clone = existingClone;
            return true;
        }

        if (cloneMap != null && cloneMap.TryGetValue(source, out targetClones))
        {
            var compatibleClone = targetClones.Values.FirstOrDefault(targetType.IsInstanceOfType);

            if (compatibleClone != null)
            {
                targetClones[targetType] = compatibleClone;
                clone = compatibleClone;
                return true;
            }
        }

        clone = null!;
        return false;
    }


    /// <summary>
    /// 记录源对象指定目标类型的克隆实例
    /// </summary>
    /// <param name="source">源对象</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="clone">克隆实例</param>
    private static void RegisterClone(object source, Type targetType, object clone)
    {
        if (!cloneMap!.TryGetValue(source, out var targetClones))
        {
            targetClones = [];
            cloneMap.Add(source, targetClones);
        }

        targetClones[targetType] = clone;
    }


    /// <summary>
    /// 替换字典内容并在失败时恢复原数据
    /// </summary>
    /// <param name="target">目标字典</param>
    /// <param name="entries">新字典条目</param>
    private static void ReplaceDictionaryContents(IDictionary target, IReadOnlyCollection<DictionaryEntry> entries)
    {
        List<DictionaryEntry> originalEntries = [];

        foreach (DictionaryEntry entry in target)
        {
            originalEntries.Add(entry);
        }

        try
        {
            target.Clear();

            foreach (DictionaryEntry entry in entries)
            {
                target.Add(entry.Key, entry.Value);
            }
        }
        catch (Exception replaceException)
        {
            try
            {
                RestoreDictionaryContents(target, originalEntries);
            }
            catch (Exception restoreException)
            {
                throw new AggregateException("字典内容替换失败，且无法恢复原数据", replaceException, restoreException);
            }

            throw;
        }

        static void RestoreDictionaryContents(IDictionary target, IReadOnlyCollection<DictionaryEntry> entries)
        {
            target.Clear();

            foreach (DictionaryEntry entry in entries)
            {
                target.Add(entry.Key, entry.Value);
            }
        }
    }


    /// <summary>
    /// 替换列表内容并在失败时恢复原数据
    /// </summary>
    /// <param name="target">目标列表</param>
    /// <param name="items">新列表元素</param>
    private static void ReplaceListContents(IList target, IReadOnlyCollection<object?> items)
    {
        List<object?> originalItems = [];

        foreach (var item in target)
        {
            originalItems.Add(item);
        }

        try
        {
            target.Clear();

            foreach (var item in items)
            {
                target.Add(item);
            }
        }
        catch (Exception replaceException)
        {
            try
            {
                RestoreListContents(target, originalItems);
            }
            catch (Exception restoreException)
            {
                throw new AggregateException("列表内容替换失败，且无法恢复原数据", replaceException, restoreException);
            }

            throw;
        }

        static void RestoreListContents(IList target, IReadOnlyCollection<object?> items)
        {
            target.Clear();

            foreach (var item in items)
            {
                target.Add(item);
            }
        }
    }


    /// <summary>
    /// 枚举数组中的全部索引组合
    /// </summary>
    /// <param name="array">数组</param>
    /// <returns>索引组合</returns>
    private static IEnumerable<int[]> EnumerateArrayIndices(Array array)
    {
        int[] indices = new int[array.Rank];

        foreach (var item in EnumerateDimension(0))
        {
            yield return item;
        }

        IEnumerable<int[]> EnumerateDimension(int dimension)
        {
            int lowerBound = array.GetLowerBound(dimension);
            int upperBound = array.GetUpperBound(dimension);

            for (int index = lowerBound; index <= upperBound; index++)
            {
                indices[dimension] = index;

                if (dimension == array.Rank - 1)
                {
                    yield return (int[])indices.Clone();
                }
                else
                {
                    foreach (var item in EnumerateDimension(dimension + 1))
                    {
                        yield return item;
                    }
                }
            }
        }
    }



    /// <summary>
    /// 获取集合元素类型
    /// </summary>
    /// <param name="type">集合类型</param>
    /// <returns></returns>
    private static Type GetListElementType(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType() ?? typeof(object);
        }

        if (type.IsGenericType)
        {
            var genericArguments = type.GetGenericArguments();

            if (genericArguments.Length > 0)
            {
                return genericArguments[0];
            }
        }

        return typeof(object);
    }



    /// <summary>
    /// 判断一个类型是否是枚举或可为空的枚举
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsEnumOrNullableEnum(Type type)
    {
        if (type.IsEnum)
        {
            return true;
        }
        else
        {
            Type underlyingType = Nullable.GetUnderlyingType(type)!;
            return underlyingType != null && underlyingType.IsEnum;
        }
    }
}
