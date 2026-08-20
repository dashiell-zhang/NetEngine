namespace Repository.Partitioning;

/// <summary>
/// 表示一个下限包含且上限不包含的雪花 ID 分区范围
/// </summary>
public sealed class PartitionRange
{

    /// <summary>
    /// 创建雪花 ID 分区范围
    /// </summary>
    /// <param name="startId">包含的起始雪花 ID</param>
    /// <param name="endId">不包含的结束雪花 ID</param>
    /// <param name="startTime">范围起始 UTC 时间</param>
    /// <param name="endTime">范围结束 UTC 时间</param>
    public PartitionRange(long startId, long endId, DateTimeOffset startTime, DateTimeOffset endTime)
    {

        StartId = startId;
        EndId = endId;
        StartTime = startTime;
        EndTime = endTime;

    }


    /// <summary>
    /// 包含的起始雪花 ID
    /// </summary>
    public long StartId { get; }


    /// <summary>
    /// 不包含的结束雪花 ID
    /// </summary>
    public long EndId { get; }


    /// <summary>
    /// 范围起始 UTC 时间
    /// </summary>
    public DateTimeOffset StartTime { get; }


    /// <summary>
    /// 范围结束 UTC 时间
    /// </summary>
    public DateTimeOffset EndTime { get; }

}
