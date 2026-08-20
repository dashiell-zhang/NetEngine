namespace Repository.Attributes;

/// <summary>
/// 声明实体对应的 PostgreSQL RANGE 分区表
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PartitionTableAttribute : Attribute
{

    /// <summary>
    /// 创建 PostgreSQL 分区表声明
    /// </summary>
    /// <param name="interval">单个子分区包含的周期数量</param>
    /// <param name="unit">分区周期单位</param>
    public PartitionTableAttribute(int interval, PartitionUnit unit)
    {

        Interval = interval;
        Unit = unit;

    }


    /// <summary>
    /// 单个子分区包含的周期数量
    /// </summary>
    public int Interval { get; }


    /// <summary>
    /// 分区周期单位
    /// </summary>
    public PartitionUnit Unit { get; }

}


/// <summary>
/// 定义 PostgreSQL 子分区使用的时间周期单位
/// </summary>
public enum PartitionUnit
{

    /// <summary>
    /// 小时
    /// </summary>
    Hour = 0,

    /// <summary>
    /// 天
    /// </summary>
    Day = 1,

    /// <summary>
    /// 自然月
    /// </summary>
    Month = 2,

    /// <summary>
    /// 自然年
    /// </summary>
    Year = 3

}
