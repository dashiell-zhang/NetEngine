namespace Repository.Core.Attributes;
/// <summary>
/// 将复杂属性映射为 JSON 数据列
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class JsonColumnAttribute : Attribute
{

}
