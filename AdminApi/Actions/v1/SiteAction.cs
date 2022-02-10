using Common;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using System;
using System.Linq;

namespace AdminApi.Actions.v1
{
    public class SiteAction
    {

        public static bool SetSiteInfo(string key, string? value)
        {

            if (value != null)
            {
                using var scope = Program.ServiceProvider.CreateScope();
                DatabaseContext db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                SnowflakeHelper snowflakeHelper = Program.ServiceProvider.GetRequiredService<SnowflakeHelper>();

                var appSetting = db.TAppSetting.Where(t => t.IsDelete == false && t.Module == "Site" && t.Key == key).FirstOrDefault();


                if (appSetting == null)
                {
                    appSetting = new TAppSetting("Site", key, value);
                    appSetting.Id = snowflakeHelper.GetId();
                    appSetting.CreateTime = DateTime.UtcNow;
                    db.TAppSetting.Add(appSetting);
                }
                else
                {
                    appSetting.Value = value;
                }

                db.SaveChanges();

            }

            return true;
        }
    }
}
