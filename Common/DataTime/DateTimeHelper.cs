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



        /// <summary>
        /// 获取某个日期，本周第一天
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public static DateTime GetWeekOne(DateTime time)
        {
            switch (time.DayOfWeek.ToString())
            {

                case "Monday":
                    {
                        return Convert.ToDateTime(time.AddDays(0).ToLongDateString());
                    }

                case "Tuesday":
                    {
                        return Convert.ToDateTime(time.AddDays(-1).ToLongDateString());
                    }

                case "Wednesday":
                    {
                        return Convert.ToDateTime(time.AddDays(-2).ToLongDateString());
                    }

                case "Thursday":
                    {
                        return Convert.ToDateTime(time.AddDays(-3).ToLongDateString());
                    }

                case "Friday":
                    {
                        return Convert.ToDateTime(time.AddDays(-4).ToLongDateString());
                    }

                case "Saturday":
                    {
                        return Convert.ToDateTime(time.AddDays(-5).ToLongDateString());
                    }

                case "Sunday":
                    {
                        return Convert.ToDateTime(time.AddDays(-6).ToLongDateString());
                    }
            }

            return default;
        }



        /// <summary>
        /// 获取某个日期，本季度第一天
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public static DateTime GetQuarterlyOne(DateTime time)
        {
            var month = time.Month;

            var startTime = Convert.ToDateTime(time.Year + "-01-01");

            if (month < 4)
            {
                return startTime;
            }
            else if (month > 3 && month < 7)
            {
                return startTime.AddMonths(3);
            }
            else if (month > 6 && month < 10)
            {
                return startTime.AddMonths(6);
            }
            else if (month > 9)
            {
                return startTime.AddMonths(9);
            }

            return default;
        }
    }
}
