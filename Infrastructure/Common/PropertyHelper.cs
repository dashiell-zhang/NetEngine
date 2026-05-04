using System.Collections;
using System.Reflection;

namespace Common;
public class PropertyHelper
{

    /// <summary>  
    /// 反射得到实体类的字段名称和值  
    /// </summary>  
    /// <typeparam name="T">实体类</typeparam>  
    /// <param name="t">实例化</param>  
    /// <returns></returns>  
    public static Dictionary<object, object?> GetProperties<T>(T t) where T : notnull, new()
    {
        Dictionary<object, object?> ret = [];

        PropertyInfo[] properties = t.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);

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

        PropertyInfo[] properties = t.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);

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

        var fields = typeof(T).GetProperties();

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
        Type type = left.GetType();

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>)) // 检查是否为 Dictionary 类型
        {

            if (left is IDictionary leftDict && right is IDictionary rightDict)
            {
                var keyType = type.GetGenericArguments()[0];
                var valueType = type.GetGenericArguments()[1];

                foreach (DictionaryEntry entry in rightDict)
                {
                    var clonedKey = Clone(entry.Key, keyType);
                    var clonedValue = Clone(entry.Value!, valueType);
                    leftDict.Add(clonedKey!, clonedValue);
                }
            }

        }
        else if (typeof(IList).IsAssignableFrom(typeof(T)) && typeof(T) != typeof(string))  // 检查T是否为集合类型
        {
            if (left is IList leftList && right is IList rightList)
            {
                var rightEnumerator = rightList.GetEnumerator();

                var elementType = GetListElementType(rightList.GetType());

                while (rightEnumerator.MoveNext())
                {
                    var clonedValue = Clone(rightEnumerator.Current, elementType);

                    leftList.Add(clonedValue);
                }
            }

        }
        else
        {
            var properties = type.GetProperties();

            foreach (var prop in properties)
            {
                if (prop.CanWrite)
                {
                    var value = prop.GetValue(right);

                    var clonedValue = Clone(value, prop.PropertyType);

                    prop.SetValue(left, clonedValue);
                }
            }
        }

        static object? Clone(object? original, Type type)
        {
            if (original == null) return null;

            if (type.IsValueType || type == typeof(string) || type == typeof(object))
            {
                return original;
            }
            else if (type.IsArray)
            {
                var sourceArray = (Array)original;
                var elementType = type.GetElementType() ?? typeof(object);
                var clonedArray = Array.CreateInstance(elementType, sourceArray.Length);

                for (int i = 0; i < sourceArray.Length; i++)
                {
                    clonedArray.SetValue(Clone(sourceArray.GetValue(i), elementType), i);
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
        Type type = typeof(T);

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

                        foreach (DictionaryEntry entry in rightDict)
                        {
                            var clonedKey = Clone(entry.Key, lKeyType, rKeyType);

                            if (entry.Value == null)
                            {
                                if (IsNullable(lValueType))
                                {
                                    leftDict.Add(clonedKey!, null);
                                }
                            }
                            else
                            {
                                var clonedValue = Clone(entry.Value, lValueType, rValueType);

                                if (clonedValue != null)
                                {
                                    leftDict.Add(clonedKey!, clonedValue);
                                }
                            }

                        }
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
        else if (typeof(IList).IsAssignableFrom(typeof(L)) && typeof(L) != typeof(string))  // 检查T是否为集合类型
        {

            if (typeof(IList).IsAssignableFrom(typeof(R)) && typeof(R) != typeof(string))
            {
                if (left is IList leftList && right is IList rightList)
                {

                    var lType = GetListElementType(leftList.GetType());
                    var rType = GetListElementType(rightList.GetType());

                    var rightEnumerator = rightList.GetEnumerator();

                    while (rightEnumerator.MoveNext())
                    {
                        if (rightEnumerator.Current == null)
                        {
                            leftList.Add(null);
                        }
                        else
                        {
                            var clonedValue = Clone(rightEnumerator.Current, lType, rType);

                            if (clonedValue != null)
                            {
                                leftList.Add(clonedValue);
                            }
                        }



                    }
                }
            }
            else
            {
                throw new Exception("左右都必须是 集合 类型");
            }
        }
        else
        {
            var lProperties = ltype.GetProperties().Where(prop => prop.CanWrite);
            var rProperties = rtype.GetProperties().Where(prop => prop.CanRead);

            foreach (var lProp in lProperties)
            {
                var rProp = rProperties.FirstOrDefault(p => p.Name == lProp.Name);

                if (rProp != null)
                {
                    object? rValue = rProp.GetValue(right);

                    Type lType = lProp.PropertyType;

                    if (rValue != null)
                    {
                        Type rType = rProp.PropertyType;

                        var clonedValue = Clone(rValue, lType, rType);

                        if (clonedValue != null || IsNullable(lType))
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

        static object? Clone(object original, Type lType, Type rType)
        {
            if (lType.IsValueType || lType == typeof(string) || lType == typeof(object) || rType.IsValueType || rType == typeof(string) || rType == typeof(object))
            {
                if (lType == rType)
                {
                    return original;
                }

                return lType.IsAssignableFrom(rType) ? original : null;
            }

            if (lType.IsArray && rType.IsArray && original is Array sourceArray)
            {
                var lElementType = lType.GetElementType() ?? typeof(object);
                var rElementType = rType.GetElementType() ?? typeof(object);
                var clonedArray = Array.CreateInstance(lElementType, sourceArray.Length);

                for (int i = 0; i < sourceArray.Length; i++)
                {
                    var item = sourceArray.GetValue(i);
                    clonedArray.SetValue(item == null ? null : Clone(item, lElementType, rElementType), i);
                }

                return clonedArray;
            }

            if (typeof(IList).IsAssignableFrom(lType) && typeof(IList).IsAssignableFrom(rType))
            {
                if (lType.IsInterface || lType.IsAbstract || lType.GetConstructor(Type.EmptyTypes) == null || Activator.CreateInstance(lType) is not IList clonedList || original is not IList sourceList)
                {
                    return lType.IsAssignableFrom(rType) ? original : null;
                }

                var lElementType = GetListElementType(lType);
                var rElementType = GetListElementType(rType);

                foreach (var item in sourceList)
                {
                    clonedList.Add(item == null ? null : Clone(item, lElementType, rElementType));
                }

                return clonedList;
            }

            if (lType.IsInterface || lType.IsAbstract || lType.GetConstructor(Type.EmptyTypes) == null)
            {
                return lType.IsAssignableFrom(rType) ? original : null;
            }

            if (lType == rType)
            {

                var cloneMethod = typeof(PropertyHelper).GetMethod("Assignment")!.MakeGenericMethod(lType);
                var clonedObject = Activator.CreateInstance(lType);
                cloneMethod.Invoke(null, [clonedObject, original]);
                return clonedObject;
            }
            else
            {
                var cloneMethod = typeof(PropertyHelper).GetMethod("AssignmentDifferentType")!.MakeGenericMethod(lType, rType);
                var clonedObject = Activator.CreateInstance(lType);
                cloneMethod.Invoke(null, [clonedObject, original]);
                return clonedObject;
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
