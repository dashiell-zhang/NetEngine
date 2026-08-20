namespace IdentifierGenerator;

/// <summary>
/// 定义雪花 ID 的持久化位布局和无外部依赖的时间换算能力
/// </summary>
public static class SnowflakeIdLayout
{

    /// <summary>
    /// 雪花纪元对应的 Unix 毫秒时间戳
    /// </summary>
    public const long EpochMilliseconds = 1640995200000L;


    /// <summary>
    /// 机器编号占用位数
    /// </summary>
    public const int MachineIdBits = 5;


    /// <summary>
    /// 数据中心编号占用位数
    /// </summary>
    public const int DataCenterIdBits = 5;


    /// <summary>
    /// 毫秒内序列号占用位数
    /// </summary>
    public const int SequenceBits = 11;


    /// <summary>
    /// 时间戳左移位数
    /// </summary>
    public const int TimestampLeftShift = SequenceBits + MachineIdBits + DataCenterIdBits;


    /// <summary>
    /// 根据 UTC 时间获取该毫秒对应的最小雪花 ID
    /// </summary>
    /// <param name="time">需要换算的时间</param>
    /// <returns>该毫秒对应的最小雪花 ID</returns>
    public static long GetMinIdByTime(DateTimeOffset time)
    {

        return (time.ToUnixTimeMilliseconds() - EpochMilliseconds) << TimestampLeftShift;

    }


    /// <summary>
    /// 获取雪花 ID 中包含的 UTC 时间
    /// </summary>
    /// <param name="id">雪花 ID</param>
    /// <returns>雪花 ID 中包含的 UTC 时间</returns>
    public static DateTimeOffset GetTimeById(long id)
    {

        var timestamp = (id >> TimestampLeftShift) + EpochMilliseconds;
        return DateTimeOffset.FromUnixTimeMilliseconds(timestamp);

    }

}
