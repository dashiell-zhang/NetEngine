using System.Reflection;

namespace Common;

/// <summary>
/// 提供对象属性读取和比较能力
/// </summary>
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
