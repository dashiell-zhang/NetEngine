using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Common
{
    public class DateTimeHelper
    {


        /// <summary>
        /// 获取某个日期所属周第一天
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static DateOnly GetWeekFirstDay(DateOnly date)
        {
            var dateTime = date.ToDateTime(new TimeOnly());

            switch (dateTime.DayOfWeek.ToString())
            {

                case "Monday":
                    {
                        return date.AddDays(0);
                    }

                case "Tuesday":
                    {
                        return date.AddDays(-1);
                    }

                case "Wednesday":
                    {
                        return date.AddDays(-2);
                    }

                case "Thursday":
                    {
                        return date.AddDays(-3);
                    }

                case "Friday":
                    {
                        return date.AddDays(-4);
                    }

                case "Saturday":
                    {
                        return date.AddDays(-5);
                    }

                case "Sunday":
                    {
                        return date.AddDays(-6);
                    }
            }

            return default;
        }



        /// <summary>
        /// 获取某个日期所属季度第一天
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static DateOnly GetQuarterlyFirstDay(DateOnly date)
        {
            return GetQuarterly(date) switch
            {
                1 => DateOnly.Parse(date.Year + "-01-01"),
                2 => DateOnly.Parse(date.Year + "-03-01"),
                3 => DateOnly.Parse(date.Year + "-07-01"),
                4 => DateOnly.Parse(date.Year + "-10-01"),
                _ => throw new Exception(),
            };
        }



        /// <summary>
        /// 获取某个日期所属季度
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static int GetQuarterly(DateOnly date)
        {
            return Convert.ToInt32(Math.Ceiling(date.Month / 3.0));
        }



        /// <summary>
        /// 获取NTP网络远程时间
        /// </summary>
        /// <returns></returns>
        public static DateTimeOffset GetNetworkTime()
        {

            string ntpServer = "ntp.tencent.com";

            var ntpData = new byte[48];

            ntpData[0] = 0x1B;

            var addresses = Dns.GetHostEntry(ntpServer).AddressList;

            IPEndPoint ipEndPoint = new(addresses[0], 123);
            Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            socket.Connect(ipEndPoint);

            socket.ReceiveTimeout = 3000;

            socket.Send(ntpData);
            socket.Receive(ntpData);
            socket.Close();

            const byte serverReplyTime = 40;

            ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);

            ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

            intPart = (uint)(((intPart & 0x000000ff) << 24) + ((intPart & 0x0000ff00) << 8) + ((intPart & 0x00ff0000) >> 8) + ((intPart & 0xff000000) >> 24));
            fractPart = (uint)(((fractPart & 0x000000ff) << 24) + ((fractPart & 0x0000ff00) << 8) + ((fractPart & 0x00ff0000) >> 8) + ((fractPart & 0xff000000) >> 24));

            var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

            var networkDateTime = (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long)milliseconds);

            return networkDateTime;
        }



        /// <summary>
        /// 通过时间格式字符串获取时间
        /// </summary>
        /// <param name="timeText">如：2022.02.02</param>
        /// <param name="format">如：yyyy.MM.dd</param>
        /// <returns></returns>
        public static DateTimeOffset GetTimeByString(string timeText, string format)
        {
            return DateTimeOffset.ParseExact(timeText, format, CultureInfo.CurrentCulture);
        }




        /// <summary>
        /// 年龄计算
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static int GetAge(DateOnly date)
        {
            var dateTime = date.ToDateTime(new TimeOnly());

            return Convert.ToInt32(Math.Ceiling((DateTime.Today.Subtract(dateTime).Days + 1) / 365.0));
        }



        /// <summary>
        /// 时间抹零
        /// </summary>
        /// <param name="dateTimeOffset"></param>
        /// <returns></returns>
        public static DateTime TimeErase(DateTime dateTime)
        {
            return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 0, 0, 0, dateTime.Kind);
        }



        /// <summary>
        /// 时间抹零
        /// </summary>
        /// <param name="dateTimeOffset"></param>
        /// <returns></returns>
        public static DateTimeOffset TimeErase(DateTimeOffset dateTimeOffset)
        {
            return new DateTimeOffset(dateTimeOffset.DateTime.Date, dateTimeOffset.Offset);
        }


    }
}
