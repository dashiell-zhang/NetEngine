using System;
using System.Collections.Generic;
using System.Text;
using Repository.WebCore;
using System.Linq;

namespace Common.UseDB
{
   public class GuidToInt
    {
        /// <summary>
        /// 根据Guid获取Int
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public static int GetInt(string guid)
        {
            using (webcoreContext db = new webcoreContext())
            {
                var info = db.TGuidToInt.Where(t => t.Guid == guid.ToLower()).FirstOrDefault() ?? new TGuidToInt();

                if (info.Id == 0)
                {
                    info.Guid = guid.ToLower();

                    var dbinfo = db.TGuidToInt.Add(info);

                    db.SaveChanges();

                    return dbinfo.Entity.Id;
                }
                else
                {
                    return info.Id;
                }
            }
        }




        /// <summary>
        /// 根据Int获取Guid
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static string GetGuid(int id)
        {
            using (webcoreContext db = new webcoreContext())
            {
                return db.TGuidToInt.Where(t => t.Id == id).Select(t => t.Guid).FirstOrDefault();
            }
        }
    }
}
