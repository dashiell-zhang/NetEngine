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

                    while (rightEnumerator.MoveNext())
                    {
                        var currentElement = rightEnumerator.Current;
                        var elementType = currentElement.GetType();
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
        public static void AssignmentDifferentType<L, R>(L left, R right) where L : notnull where R : notnull
        {
            Type ltype = left.GetType();
            Type rtype = right.GetType();

            var lProperties = ltype.GetProperties().Where(prop => prop.CanWrite);
            var rProperties = rtype.GetProperties().Where(prop => prop.CanRead);

            foreach (var lProp in lProperties)
            {
                var rProp = rProperties.FirstOrDefault(p => p.Name == lProp.Name && p.PropertyType == lProp.PropertyType);
                if (rProp != null)
                {
                    object? rValue = rProp.GetValue(right);

                    if (rValue != null)
                    {
                        Type type = lProp.PropertyType;

                        if (type.IsValueType || type == typeof(string))
                        {
                            lProp.SetValue(left, rValue);
                        }
                        else
                        {
                            var cloneMethod = typeof(PropertyHelper).GetMethod("Assignment")!.MakeGenericMethod(type);

                            var clonedObject = Activator.CreateInstance(type);

                            cloneMethod.Invoke(null, [clonedObject, rValue]);

                            lProp.SetValue(left, clonedObject);
                        }
                    }
                }
            }
        }

    }
}
