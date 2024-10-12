using Common;
using DistributedLock;
using IdentifierGenerator.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace IdentifierGenerator
{
    public class IdService
    {

        public IdService(IOptionsMonitor<IdSetting> config, IDistributedLock distributedLock, IDistributedCache distributedCache)
        {

            if (config.CurrentValue.DataCenterId == null || config.CurrentValue.MachineId == null)
            {
                using (distributedLock.Lock("IdentifierGenerator"))
                {
                    Random rand = new();

                    string key = "";

                    do
                    {
                        int combinedNumber = rand.Next(0, 1025);

                        config.CurrentValue.DataCenterId = (combinedNumber >> 5) & 31; //dataCenterId 取组合数右移5位后的低5位
                        config.CurrentValue.MachineId = combinedNumber & 31; //machineId 取组合数的低5位

                        key = "IdentifierGenerator-" + config.CurrentValue.DataCenterId + ":" + config.CurrentValue.MachineId;

                    } while (distributedCache.IsContainKey(key));

                    distributedCache.Set(key, "", TimeSpan.FromDays(7));
                }
            }

            if (config.CurrentValue.DataCenterId != null && config.CurrentValue.MachineId != null)
            {
                dataCenterId = config.CurrentValue.DataCenterId.Value;
                machineId = config.CurrentValue.MachineId.Value;

                maxMachineId = -1L ^ -1L << (int)machineIdBits;
                maxDataCenterId = -1L ^ -1L << (int)dataCenterIdBits;
                machineIdShift = sequenceBits;
                dataCenterIdShift = sequenceBits + machineIdBits;
                timestampLeftShift = sequenceBits + machineIdBits + dataCenterIdBits;
                sequenceMask = -1L ^ -1L << (int)sequenceBits;


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
        private long sequence = 0L;//计数从零开始
        private long lastTimestamp = -1L;//最后时间戳

        //twepoch 1640995200000L 为 UTC 2022-01-01 00:00 , sequenceBits 调整为11位，所以时间戳可用 42位，未来139年 可用
        private readonly long twepoch = 1640995200000L; //唯一时间随机量，这是一个避免重复的随机量，自行设定不要大于当前时间戳

        private readonly long machineIdBits = 5L; //机器码字节数
        private readonly long dataCenterIdBits = 5L;//数据字节数
        private readonly long maxMachineId; //最大机器ID
        private readonly long maxDataCenterId;//最大数据ID
        private readonly long sequenceBits = 11L; //计数器字节数，11个字节用来保存计数码，每毫秒可以生成2047个ID
        private readonly long machineIdShift; //机器码数据左移位数，就是后面计数器占用的位数
        private readonly long dataCenterIdShift;
        private readonly long timestampLeftShift;//时间戳左移动位数就是机器码+计数器总字节数+数据字节数
        private readonly long sequenceMask; //一微秒内可以产生计数，如果达到该值则等到下一微妙在进行生成
        private readonly object syncRoot = new();//加锁对象



        /// <summary>
        /// 获取一个ID
        /// </summary>
        /// <returns></returns>
        public long GetId()
        {
            lock (syncRoot)
            {
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                if (lastTimestamp == timestamp)
                {
                    //同一微妙中生成ID
                    sequence = sequence + 1 & sequenceMask; //用&运算计算该微秒内产生的计数是否已经到达上限
                    if (sequence == 0)
                    {
                        //一微妙内产生的ID计数已达上限，等待下一微妙
                        int i = 10;

                        do
                        {
                            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            i--;
                        } while (timestamp <= lastTimestamp && i > 0);
                    }
                }
                else
                {
                    //不同微秒生成ID
                    sequence = 0L;
                }

                if (timestamp < lastTimestamp)
                {
                    throw new Exception("时间戳比上一次生成ID时时间戳还小，故异常");
                }

                lastTimestamp = timestamp; //把当前时间戳保存为最后生成ID的时间戳

                long Id = timestamp - twepoch << (int)timestampLeftShift
                    | dataCenterId << (int)dataCenterIdShift
                    | machineId << (int)machineIdShift
                    | sequence;
                return Id;
            }
        }



        /// <summary>
        /// 通过ID获取其中的时间戳
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public DateTimeOffset GetTimeById(long id)
        {
            var idStr2 = Convert.ToString(id, 2);

            if (idStr2.Length < 63)
            {
                do
                {
                    idStr2 = "0" + idStr2;
                } while (idStr2.Length != 63);
            }

            var timeBits = 64 - machineIdBits - dataCenterIdBits - sequenceBits - 1;

            var timeStr2 = idStr2[..Convert.ToInt32(timeBits)];

            var timeJsStamp = Convert.ToInt64(timeStr2, 2);

            var twepochTime = DateTimeOffset.FromUnixTimeMilliseconds(twepoch);

            DateTime startTime = new(twepochTime.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return startTime.AddMilliseconds(timeJsStamp);
        }



        /// <summary>
        /// 获取指定时间开始的最小Id
        /// </summary>
        /// <returns></returns>
        public long GetMinIdByTime(DateTimeOffset startTime)
        {
            long timestamp = startTime.ToUnixTimeMilliseconds();

            lastTimestamp = timestamp; //把当前时间戳保存为最后生成ID的时间戳
            long Id = timestamp - twepoch << (int)timestampLeftShift
                | 0L << (int)dataCenterIdShift
                | 0L << (int)machineIdShift
                | 0;
            return Id;
        }

    }
}
