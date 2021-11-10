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
            var db = Program.ServiceProvider.CreateScope().ServiceProvider.GetService<dbContext>();

            var appSetting = db.TAppSetting.Where(t => t.IsDelete == false & t.Module == "Site" & t.Key == key).FirstOrDefault() ?? new TAppSetting();

            appSetting.Value = value;

            if (appSetting.Id == default)
            {
                appSetting.Id = Guid.NewGuid();
                appSetting.CreateTime = DateTime.Now;
                appSetting.Module = "Site";
                appSetting.Key = key;
                db.TAppSetting.Add(appSetting);
            }

            db.SaveChanges();

            return true;
        }
    }
}
