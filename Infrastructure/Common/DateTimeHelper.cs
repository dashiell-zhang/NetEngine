using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Common;

/// <summary>
/// 提供日期、时间和网络时间处理能力
/// </summary>
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
            1 => new DateOnly(date.Year, 1, 1),
            2 => new DateOnly(date.Year, 4, 1),
            3 => new DateOnly(date.Year, 7, 1),
            4 => new DateOnly(date.Year, 10, 1),
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



#if !BROWSER
    /// <summary>
    /// 获取NTP网络远程时间
    /// </summary>
    /// <returns></returns>
    public static DateTimeOffset GetNetworkTime()
    {
        string ntpServer = "ntp.tencent.com";

        var ntpData = new byte[48];

        ntpData[0] = 0x1B;

        var address = Dns.GetHostEntry(ntpServer).AddressList.FirstOrDefault(t => t.AddressFamily == AddressFamily.InterNetwork);

        if (address == null)
        {
            throw new Exception("未获取到可用的NTP IPv4地址");
        }

        IPEndPoint ipEndPoint = new(address, 123);
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        socket.Connect(ipEndPoint);

        socket.ReceiveTimeout = 3000;

        socket.Send(ntpData);
        int receivedLength = socket.Receive(ntpData);

        if (receivedLength < ntpData.Length)
        {
            throw new InvalidDataException("NTP响应长度不足48字节");
        }

        int leapIndicator = ntpData[0] >> 6;
        int mode = ntpData[0] & 0x07;
        int stratum = ntpData[1];

        if (leapIndicator == 3 || mode != 4 || stratum is < 1 or > 15)
        {
            throw new InvalidDataException("NTP响应状态无效");
        }

        const byte serverReplyTime = 40;

        ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);

        ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

        intPart = (uint)(((intPart & 0x000000ff) << 24) + ((intPart & 0x0000ff00) << 8) + ((intPart & 0x00ff0000) >> 8) + ((intPart & 0xff000000) >> 24));
        fractPart = (uint)(((fractPart & 0x000000ff) << 24) + ((fractPart & 0x0000ff00) << 8) + ((fractPart & 0x00ff0000) >> 8) + ((fractPart & 0xff000000) >> 24));

        if (intPart == 0 && fractPart == 0)
        {
            throw new InvalidDataException("NTP响应未包含服务器发送时间");
        }

        const ulong ntpEraSeconds = 0x100000000UL;
        DateTimeOffset ntpEpoch = new(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);
        ulong currentNtpSeconds = (ulong)(DateTimeOffset.UtcNow - ntpEpoch).TotalSeconds;
        ulong era = currentNtpSeconds / ntpEraSeconds;
        ulong seconds = intPart + era * ntpEraSeconds;

        if (seconds > currentNtpSeconds + ntpEraSeconds / 2 && seconds >= ntpEraSeconds)
        {
            seconds -= ntpEraSeconds;
        }
        else if (seconds + ntpEraSeconds / 2 < currentNtpSeconds)
        {
            seconds += ntpEraSeconds;
        }

        var milliseconds = (seconds * 1000) + ((fractPart * 1000) / ntpEraSeconds);
        var networkDateTime = ntpEpoch.AddMilliseconds((long)milliseconds);

        return networkDateTime;
    }
#endif



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
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - date.Year;

        if (today < date.AddYears(age))
        {
            age--;
        }

        return Math.Max(age, 0);
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



    /// <summary>
    /// 获取GuidV7中的时间信息
    /// </summary>
    /// <param name="guid"></param>
    /// <returns></returns>
    public static DateTimeOffset GetTimeFromGuidV7(Guid guid)
    {
        // 将GUID转换为字符串，这样可以获得标准格式
        string guidString = guid.ToString("N");

        if (guidString[12] != '7')
        {
            throw new ArgumentException("输入Guid不是Guid V7", nameof(guid));
        }

        // 从字符串中提取前12个字符（对应前6个字节）
        string timestampHex = guidString[..12];

        // 将十六进制字符串转换为长整型
        long timestamp = Convert.ToInt64(timestampHex, 16);

        return DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
    }

}
