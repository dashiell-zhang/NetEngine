using System;

namespace Common
{

    /// <summary>
    /// 利用雪花算法动态生成有序的ID
    /// </summary>
    public class SnowflakeHelper
    {


        private static long machineId;//机器ID
        private static long datacenterId = 0L;//数据ID
        private static long sequence = 0L;//计数从零开始
        private static long lastTimestamp = -1L;//最后时间戳

        //twepoch 1609459200000L 为 UTC 2021-01-01 00:00 , sequenceBits 调整为11位，所以时间戳可用 42位，未来139年 可用
        private readonly static long twepoch = 1609459200000L; //唯一时间随机量，这是一个避免重复的随机量，自行设定不要大于当前时间戳
        private readonly static long machineIdBits = 5L; //机器码字节数
        private readonly static long datacenterIdBits = 5L;//数据字节数
        private readonly static long maxMachineId = -1L ^ -1L << (int)machineIdBits; //最大机器ID
        private readonly static long maxDatacenterId = -1L ^ (-1L << (int)datacenterIdBits);//最大数据ID
        private readonly static long sequenceBits = 11L; //计数器字节数，11个字节用来保存计数码，每毫秒可以生成2047个ID
        private readonly static long machineIdShift = sequenceBits; //机器码数据左移位数，就是后面计数器占用的位数
        private readonly static long datacenterIdShift = sequenceBits + machineIdBits;
        private readonly static long timestampLeftShift = sequenceBits + machineIdBits + datacenterIdBits; //时间戳左移动位数就是机器码+计数器总字节数+数据字节数
        private readonly static long sequenceMask = -1L ^ -1L << (int)sequenceBits; //一微秒内可以产生计数，如果达到该值则等到下一微妙在进行生成
        private readonly static object syncRoot = new();//加锁对象



        /// <summary>
        /// 初始化雪花ID模块
        /// </summary>
        /// <param name="machineId">机器ID,最大为31</param>
        /// <param name="datacenterId">数据ID,最大为31</param>
        public SnowflakeHelper(long machineId, long datacenterId)
        {
            Snowflakes(machineId, datacenterId);
        }



        private static void Snowflakes(long machineId, long datacenterId)
        {
            if (machineId < 0 || machineId > maxMachineId)
            {
                throw new Exception("机器码ID非法");
            }
            else
            {
                SnowflakeHelper.machineId = machineId;
            }

            if (datacenterId < 0 || datacenterId > maxDatacenterId)
            {
                throw new Exception("数据中心ID非法");
            }
            else
            {
                SnowflakeHelper.datacenterId = datacenterId;
            }
        }





        /// <summary>
        /// 获取下一微秒时间戳
        /// </summary>
        /// <param name="lastTimestamp"></param>
        /// <returns></returns>
        private static long GetNextTimestamp(long lastTimestamp)
        {
            long timestamp = DateTimeHelper.TimeToJs();
            if (timestamp <= lastTimestamp)
            {
                timestamp = DateTimeHelper.TimeToJs();
            }
            return timestamp;
        }




        /// <summary>
        /// 获取一个雪花ID
        /// </summary>
        /// <returns></returns>
#pragma warning disable CA1822 // 将成员标记为 static
        public long GetId()
#pragma warning restore CA1822 // 将成员标记为 static
        {
            lock (syncRoot)
            {
                long timestamp = DateTimeHelper.TimeToJs();
                if (lastTimestamp == timestamp)
                { //同一微妙中生成ID
                    sequence = (sequence + 1) & sequenceMask; //用&运算计算该微秒内产生的计数是否已经到达上限
                    if (sequence == 0)
                    {
                        //一微妙内产生的ID计数已达上限，等待下一微妙
                        timestamp = GetNextTimestamp(lastTimestamp);
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
                long Id = ((timestamp - twepoch) << (int)timestampLeftShift)
                    | (datacenterId << (int)datacenterIdShift)
                    | (machineId << (int)machineIdShift)
                    | sequence;
                return Id;
            }
        }



        /// <summary>
        /// 通过ID获取其中的时间戳
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public DateTime GetTimeById(long id)
        {
            var idStr2 = Convert.ToString(id, 2);

            if (idStr2.Length < 63)
            {
                do
                {
                    idStr2 = "0" + idStr2;
                } while (idStr2.Length != 63);
            }

            var timeBits = 64 - machineIdBits - datacenterIdBits - sequenceBits - 1;

            var timeStr2 = idStr2[..Convert.ToInt32(timeBits)];

            var timeJsStamp = Convert.ToInt64(timeStr2, 2);

            var twepochTime = DateTimeHelper.JsToTime(twepoch);

            var time = DateTimeHelper.JsToTime(timeJsStamp, twepochTime.Year);

            return time;
        }


    }
}
