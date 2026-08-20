namespace Repository.Partitioning;

/// <summary>
/// 集中定义分区模型使用的 EF Core Annotation 名称
/// </summary>
public static class PartitionAnnotationNames
{

    /// <summary>
    /// 分区策略 Annotation
    /// </summary>
    public const string Strategy = "PartitionTable:Strategy";


    /// <summary>
    /// 分区键数据库列名称 Annotation
    /// </summary>
    public const string KeyColumn = "PartitionTable:KeyColumn";


    /// <summary>
    /// 分区键类型 Annotation
    /// </summary>
    public const string KeyType = "PartitionTable:KeyType";


    /// <summary>
    /// 分区间隔小时数 Annotation
    /// </summary>
    public const string IntervalHours = "PartitionTable:IntervalHours";


    /// <summary>
    /// 当前固定使用的 PostgreSQL RANGE 分区策略值
    /// </summary>
    public const string RangeStrategy = "Range";


    /// <summary>
    /// 分区键使用雪花 ID 布局
    /// </summary>
    public const string SnowflakeIdKeyType = "SnowflakeId";


    /// <summary>
    /// 获取全部分区 Annotation 名称
    /// </summary>
    /// <returns>全部分区 Annotation 名称</returns>
    public static IReadOnlyList<string> GetAll()
    {

        return [Strategy, KeyColumn, KeyType, IntervalHours];

    }

}
