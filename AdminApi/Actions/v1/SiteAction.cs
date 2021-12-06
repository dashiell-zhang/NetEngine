using Common;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using System;
using System.Linq;

namespace AdminApi.Actions.v1
{
    public class SiteAction
    {

        public static bool SetSiteInfo(string key, string value)
        {
            var db = Program.ServiceProvider.GetService<dbContext>();

            var snowflakeHelper = Program.ServiceProvider.GetService<SnowflakeHelper>();

            var appSetting = db.TAppSetting.Where(t => t.IsDelete == false & t.Module == "Site" & t.Key == key).FirstOrDefault() ?? new TAppSetting();

            appSetting.Value = value;

            if (appSetting.Id == default)
            {
                appSetting.Id = snowflakeHelper.GetId();
                appSetting.CreateTime = DateTime.UtcNow;
                appSetting.Module = "Site";
                appSetting.Key = key;
                db.TAppSetting.Add(appSetting);
            }

            db.SaveChanges();

            return true;
        }
    }
}
