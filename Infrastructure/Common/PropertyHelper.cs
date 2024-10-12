using System.Collections;
using System.Reflection;

namespace Common
{
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

                string? oldValue = pi.GetValue(original)?.ToString();
                string? newValue = pi.GetValue(after)?.ToString();

                string typename = pi.PropertyType.FullName!;

                if ((typename != "System.Decimal" && oldValue != newValue) || (typename == "System.Decimal" && decimal.Parse(oldValue!) != decimal.Parse(newValue!)))
                {

                    retValue += pi.Name + ":";

                    if (typename == "System.Boolean")
                    {
                        retValue += (bool.Parse(oldValue!) ? "是" : "否") + " -> ";
                        retValue += (bool.Parse(newValue!) ? "是" : "否") + "； \n";
                    }
                    else if (typename == "System.DateTime")
                    {
                        retValue += (oldValue != null ? DateTime.Parse(oldValue).ToString("yyyy-MM-dd") : "") + " ->";
                        retValue += (newValue != null ? DateTime.Parse(newValue).ToString("yyyy-MM-dd") : "") + "； \n";
                    }
                    else
                    {
                        retValue += (oldValue ?? "") + " -> ";
                        retValue += (newValue ?? "") + "； \n";
                    }
                }
            }

            return retValue;
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
                var leftDict = left as IDictionary;
                var rightDict = right as IDictionary;

                if (leftDict != null && rightDict != null)
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
                var leftList = left as IList;
                var rightList = right as IList;

                if (leftList != null && rightList != null)
                {
                    var rightEnumerator = rightList.GetEnumerator();

                    var elementType = rightList.GetType().GetGenericArguments()[0];

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

                if (type.IsValueType || type == typeof(string))
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
                    var leftDict = left as IDictionary;
                    var rightDict = right as IDictionary;

                    if (leftDict != null && rightDict != null)
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

                    var leftList = left as IList;
                    var rightList = right as IList;

                    if (leftList != null && rightList != null)
                    {

                        var lType = leftList.GetType().GetGenericArguments()[0];
                        var rType = rightList.GetType().GetGenericArguments()[0];

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
                if (lType.IsValueType || lType == typeof(string) || rType.IsValueType || rType == typeof(string))
                {
                    if (lType == rType)
                    {
                        return original;
                    }
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
}
