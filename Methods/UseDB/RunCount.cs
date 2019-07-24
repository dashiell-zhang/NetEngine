using System;
using System.Collections.Generic;
using System.Text;
using Models.DataBases.WebCore;
using System.Linq;

namespace Methods.UseDB
{
    /// <summary>
    /// 计数方法
    /// </summary>
    public class RunCount
    {


        /// <summary>
        /// 获取指定类型的计数
        /// </summary>
        /// <param name="tag">标签</param>
        /// <returns></returns>
        public static int Add(string tag)
        {
            using (webcoreContext db = new webcoreContext())
            {

                var info = db.TCount.Where(t => t.Tag == tag).FirstOrDefault() ?? new TCount();

                if (!string.IsNullOrEmpty(info.Id))
                {
                    info.Count = info.Count + 1;
                    info.UpdateTime = DateTime.Now;

                    db.SaveChanges();

                    return info.Count;
                }
                else
                {
                    info.Id = Guid.NewGuid().ToString();
                    info.Tag = tag;
                    info.Count = 1;
                    info.CreateTime = DateTime.Now;

                    db.TCount.Add(info);

                    db.SaveChanges();

                    return info.Count;

                }

            }
        }




        /// <summary>
        /// 通过类型获取计数值
        /// </summary>
        /// <param name="tag">标记</param>
        /// <param name="starttime">开始时间</param>
        /// <param name="endtime">结束时间</param>
        /// <returns></returns>
        public static int Get(string tag, DateTime starttime = default(DateTime), DateTime endtime = default(DateTime))
        {

            using (webcoreContext db = new webcoreContext())
            {
                var query = db.TCount.Where(t => t.Tag.Contains(tag));

                if (starttime != default(DateTime) & endtime != default(DateTime))
                {
                    query = query.Where(t => t.CreateTime >= starttime & t.CreateTime <= endtime);
                }

                return query.ToList().Sum(t => t.Count);
            }

        }

    }
}
