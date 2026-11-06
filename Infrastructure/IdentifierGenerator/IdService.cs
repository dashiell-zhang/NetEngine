using IdentifierGenerator.Models;

namespace IdentifierGenerator;

/// <summary>
/// 生成使用固定雪花布局的全局唯一 ID
/// </summary>
public class IdService : IDisposable, IAsyncDisposable
{

    /// <summary>
    /// 创建由宿主明确分配且保证不重复的固定节点
    /// </summary>
    public IdService(int dataCenterId, int machineId)
    {

        if (dataCenterId is < 0 or > 31 || machineId is < 0 or > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(dataCenterId), "雪花节点编号必须在 0 到 31 之间");
        }
        this.dataCenterId = dataCenterId;
        this.machineId = machineId;
        sequenceMask = (1L << SnowflakeIdLayout.SequenceBits) - 1;
        machineIdShift = SnowflakeIdLayout.SequenceBits;
        dataCenterIdShift = SnowflakeIdLayout.SequenceBits + SnowflakeIdLayout.MachineIdBits;

    }


    /// <summary>
    /// 使用已确认且正在自动续期的共享节点
    /// </summary>
    private IdService(RedisNodeLease lease) : this(lease.Node >> 5, lease.Node & 31)
    {

        this.lease = lease;

    }


    /// <summary>
    /// 原子取得共享节点后创建可发号服务
    /// </summary>
    internal static IdService CreateAutomatic(IdSetting settings)
    {

        return new IdService(RedisNodeLease.AcquireAsync(settings).GetAwaiter().GetResult());

    }


    private readonly RedisNodeLease? lease;
    private int disposed;
    private readonly long machineId;
    private readonly long dataCenterId;
    private long sequence = 0L; //计数从零开始
    private long lastTimestamp = -1L; //最后时间戳

    private readonly long sequenceMask;

    private readonly long machineIdShift;
    private readonly long dataCenterIdShift;
    // 使用 object 作为锁对象是更通用的做法
    private readonly object syncRoot = new object();


    /// <summary>
    /// 获取一个ID
    /// </summary>
    /// <returns></returns>
    public long GetId()
    {
        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            lease?.EnsureValid();
            long timestamp = GetCurrentTimestamp();

            // 当发生时钟回拨时，抛出异常
            if (timestamp < lastTimestamp)
            {
                throw new Exception($"时钟回拨。拒绝为 {lastTimestamp - timestamp} 毫秒生成ID");
            }

            // 如果是同一毫秒内，则进行序列号递增
            if (lastTimestamp == timestamp)
            {
                // 通过位运算增加序列号，并防止溢出
                sequence = (sequence + 1) & sequenceMask;
                // 如果序列号溢出，则等待到下一毫秒
                if (sequence == 0)
                {
                    timestamp = WaitNextMillis(lastTimestamp);
                }
            }
            else
            {
                // 时间戳改变，序列号重置
                sequence = 0L;
            }

            lease?.EnsureValid();

            // 更新最后的时间戳
            lastTimestamp = timestamp;

            // 通过位运算拼接ID
            // 逻辑：(时间戳 - 起始时间) 左移 | 数据中心ID 左移 | 机器ID 左移 | 序列号
            return ((timestamp - SnowflakeIdLayout.EpochMilliseconds) << SnowflakeIdLayout.TimestampLeftShift)
                   | (dataCenterId << (int)dataCenterIdShift)
                   | (machineId << (int)machineIdShift)
                   | sequence;
        }
    }


    /// <summary>
    /// 通过ID获取其中的UTC时间
    /// </summary>
    /// <param name="id">雪花ID</param>
    /// <returns>ID中包含的UTC时间</returns>
    public DateTimeOffset GetTimeById(long id)
    {
        return SnowflakeIdLayout.GetTimeById(id);
    }


    /// <summary>
    /// 获取指定时间开始的最小Id
    /// </summary>
    /// <param name="startTime">指定的起始时间</param>
    /// <returns>该毫秒对应的最小ID</returns>
    public long GetMinIdByTime(DateTimeOffset startTime)
    {
        return SnowflakeIdLayout.GetMinIdByTime(startTime);
    }


    /// <summary>
    /// 阻塞到下一个毫秒，直到获得新的时间戳
    /// </summary>
    /// <param name="lastTimestamp">上一次生成ID的时间戳</param>
    /// <returns>新的时间戳</returns>
    private long WaitNextMillis(long lastTimestamp)
    {
        long timestamp = GetCurrentTimestamp();
        while (timestamp <= lastTimestamp)
        {
            // 选择短暂休眠来避免CPU空转，对于高并发场景更为友好
            Thread.Sleep(1);
            timestamp = GetCurrentTimestamp();
        }
        return timestamp;
    }


    /// <summary>
    /// 获取当前UTC时间戳（毫秒）
    /// </summary>
    /// <returns></returns>
    private long GetCurrentTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }


    /// <summary>
    /// 停止发号并释放自动节点续期资源
    /// </summary>
    public void Dispose()
    {

        DisposeAsync().AsTask().GetAwaiter().GetResult();

    }


    /// <summary>
    /// 异步停止自动节点续期，保留远端节点占用至到期
    /// </summary>
    public async ValueTask DisposeAsync()
    {

        if (Interlocked.Exchange(ref disposed, 1) == 0 && lease is not null)
        {
            await lease.DisposeAsync().ConfigureAwait(false);
        }

    }

}
