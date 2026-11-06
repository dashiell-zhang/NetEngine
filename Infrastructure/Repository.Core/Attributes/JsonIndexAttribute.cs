namespace Repository.Core.Attributes;
/// <summary>
/// 声明 JSON 数据列内部字段的索引
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class JsonIndexAttribute : Attribute
{

    /// <summary>
    /// 是否创建唯一索引
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// 是否对所有索引字段降序排列
    /// </summary>
    public bool AllDescending { get; set; }

    /// <summary>
    /// 各索引字段的降序设置
    /// </summary>
    public bool[]? IsDescending { get; set; }


    /// <summary>
    /// 索引包含的字段名称
    /// </summary>
    public List<string> PropertyNames = [];


    /// <summary>
    /// 创建字段名称不重复的 JSON 索引声明
    /// </summary>
    public JsonIndexAttribute(params string[] additionalPropertyNames)
    {
        var ss = additionalPropertyNames.Distinct().Count();

        if (ss != additionalPropertyNames.Length)
        {
            throw new ArgumentException("additionalPropertyNames 不允许存在重复的值");
        }

        PropertyNames.AddRange(additionalPropertyNames);
    }

}
