using Common;
using DistributedLock;
using IdentifierGenerator.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace IdentifierGenerator;
public class IdService
{

    public IdService(IOptionsMonitor<IdSetting> config, IDistributedLock distributedLock, IDistributedCache distributedCache)
    {
        if (config.CurrentValue.DataCenterId == null || config.CurrentValue.MachineId == null)
        {
            using (distributedLock.LockAsync("IdentifierGenerator").Result)
            {
                Random rand = new();
                string key = "";
                int tryCount = 0;
                do
                {
                    tryCount++;
                    if (tryCount > 10)
                    {
                        throw new Exception("Id生成器注册DataCenterId和MachineId失败");
                    }
                    int combinedNumber = rand.Next(0, 1025); // 1024 = 2^10
                    config.CurrentValue.DataCenterId = (combinedNumber >> 5) & 31; // dataCenterId 取组合数右移5位后的低5位
                    config.CurrentValue.MachineId = combinedNumber & 31; // machineId 取组合数的低5位
                    key = "IdentifierGenerator-" + config.CurrentValue.DataCenterId + ":" + config.CurrentValue.MachineId;
                } while (distributedCache.IsContainKey(key));
                distributedCache.Set(key, "", TimeSpan.FromHours(1));
            }
        }

        if (config.CurrentValue.DataCenterId != null && config.CurrentValue.MachineId != null)
        {
            dataCenterId = config.CurrentValue.DataCenterId.Value;
            machineId = config.CurrentValue.MachineId.Value;

            // 使用位运算直接计算掩码和最大值，更高效且清晰
            maxMachineId = -1L ^ (-1L << (int)machineIdBits);
            maxDataCenterId = -1L ^ (-1L << (int)dataCenterIdBits);
            sequenceMask = -1L ^ (-1L << (int)sequenceBits);

            // 计算移位数
            machineIdShift = sequenceBits;
            dataCenterIdShift = sequenceBits + machineIdBits;
            timestampLeftShift = sequenceBits + machineIdBits + dataCenterIdBits;

            if (dataCenterId < 0 || dataCenterId > maxDataCenterId)
            {
                throw new Exception("数据中心ID异常");
            }
            if (machineId < 0 || machineId > maxMachineId)
            {
                throw new Exception("机器码ID异常");
            }
        }
        else
        {
            throw new Exception("数据中心ID或机器码ID异常");
        }
    }


    private readonly long machineId;
    private readonly long dataCenterId;
    private long sequence = 0L; //计数从零开始
    private long lastTimestamp = -1L; //最后时间戳

    private readonly long twepoch = 1640995200000L; // UTC 2022-01-01 00:00:00

    private readonly long machineIdBits = 5L;
    private readonly long dataCenterIdBits = 5L;
    private readonly long sequenceBits = 11L;

    private readonly long maxMachineId;
    private readonly long maxDataCenterId;
    private readonly long sequenceMask;

    private readonly long machineIdShift;
    private readonly long dataCenterIdShift;
    private readonly long timestampLeftShift;

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

            // 更新最后的时间戳
            lastTimestamp = timestamp;

            // 通过位运算拼接ID
            // 逻辑：(时间戳 - 起始时间) 左移 | 数据中心ID 左移 | 机器ID 左移 | 序列号
            return ((timestamp - twepoch) << (int)timestampLeftShift)
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
        long timestamp = (id >> (int)timestampLeftShift) + twepoch;
        return DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
    }


    /// <summary>
    /// 获取指定时间开始的最小Id
    /// </summary>
    /// <param name="startTime">指定的起始时间</param>
    /// <returns>该毫秒对应的最小ID</returns>
    public long GetMinIdByTime(DateTimeOffset startTime)
    {
        long timestamp = startTime.ToUnixTimeMilliseconds();

        // 最小ID即为该时间戳左移相应位数，数据中心、机器码和序列号部分都为0。

        return (timestamp - twepoch) << (int)timestampLeftShift;
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
}
