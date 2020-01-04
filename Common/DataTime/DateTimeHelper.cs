using System;
using System.Collections.Generic;
using System.Text;

namespace Common.DataTime
{
    public class DateTimeHelper
    {


        /// <summary>
        /// unix时间戳转DateTime
        /// </summary>
        /// <param name="unix"></param>
        /// <returns></returns>
        public static DateTime UnixToTime(long unix)
        {
            System.DateTime startTime = TimeZoneInfo.ConvertTimeToUtc((new DateTime(1970, 1, 1)).ToLocalTime()).ToLocalTime(); // 当地时区
            return startTime.AddSeconds(unix);
        }




        /// <summary>
        /// DateTime转unix时间戳
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public static long TimeToUnix(DateTime time)
        {
            System.DateTime startTime = TimeZoneInfo.ConvertTimeToUtc((new DateTime(1970, 1, 1)).ToLocalTime()).ToLocalTime(); // 当地时区
            return (long)(time - startTime).TotalSeconds; // 相差秒数
        }



        /// <summary>
        /// JavaScript时间戳转DateTime
        /// </summary>
        /// <param name="unix"></param>
        /// <returns></returns>
        public static DateTime JsToTime(long js)
        {
           
            System.DateTime startTime = TimeZoneInfo.ConvertTimeToUtc((new DateTime(1970, 1, 1)).ToLocalTime()).ToLocalTime(); // 当地时区
            return startTime.AddMilliseconds(js);
        }




        /// <summary>
        /// DateTime转Js时间戳
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public static long TimeToJs(DateTime time)
        {
            System.DateTime startTime = TimeZoneInfo.ConvertTimeToUtc((new DateTime(1970, 1, 1)).ToLocalTime()).ToLocalTime(); // 当地时区
            return (long)(time - startTime).TotalMilliseconds;
        }


    }
}
