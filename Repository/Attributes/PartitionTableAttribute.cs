namespace Repository.Attributes;

/// <summary>
/// 声明实体对应的 PostgreSQL RANGE 分区表
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PartitionTableAttribute : Attribute
{

    /// <summary>
    /// 单个子分区包含的小时数
    /// </summary>
    public int IntervalHours { get; set; }

}
