namespace Repository.Partitioning;

/// <summary>
/// 集中定义 PostgreSQL 时间分区使用的固定 UTC+8 时间布局
/// </summary>
internal static class PartitionTimeLayout
{

    /// <summary>
    /// 分区时间相对 UTC 的固定偏移量
    /// </summary>
    internal static readonly TimeSpan Offset = TimeSpan.FromHours(8);


    /// <summary>
    /// 多周期分区计算使用的固定对齐锚点
    /// </summary>
    internal static readonly DateTimeOffset Anchor = new(1970, 1, 1, 0, 0, 0, Offset);


    /// <summary>
    /// 将任意绝对时间转换为固定 UTC+8 时间
    /// </summary>
    /// <param name="time">需要转换的时间</param>
    /// <returns>固定 UTC+8 表示的同一绝对时间</returns>
    internal static DateTimeOffset ToPartitionTime(DateTimeOffset time)
    {

        return time.ToOffset(Offset);

    }

}
