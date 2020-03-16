using System;
using System.Collections.Generic;
using System.Text;
using Repository.WebCore;

namespace Common.UseDB
{
    public static class Log
    {


        /// <summary>
        /// 保存日志信息
        /// </summary>
        /// <param name="Sign">自定义标记</param>
        /// <param name="Type">日志类型</param>
        /// <param name="Content">日志内容</param>
        /// <returns></returns>
        public static bool Set(string Sign, string Type, string Content)
        {
            try
            {
                using (var db = new webcoreContext())
                {
                    var log = new TLog();

                    log.Id = Guid.NewGuid().ToString();
                    log.Sign = Sign;
                    log.Type = Type;
                    log.Content = Content;
                    log.CreateTime = DateTime.Now;

                    db.TLog.Add(log);

                    db.SaveChanges();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
