using Common;
using Repository.Database;

namespace AdminApi.Services
{

    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class SiteService
    {


        private readonly DatabaseContext db;
        private readonly SnowflakeHelper snowflakeHelper;

        public SiteService(DatabaseContext db, SnowflakeHelper snowflakeHelper)
        {
            this.db = db;
            this.snowflakeHelper = snowflakeHelper;
        }


        public bool SetSiteInfo(string key, string? value)
        {

            if (value != null)
            {

                var appSetting = db.TAppSetting.Where(t => t.IsDelete == false && t.Module == "Site" && t.Key == key).FirstOrDefault();

                if (appSetting == null)
                {
                    appSetting = new()
                    {
                        Id = snowflakeHelper.GetId(),
                        Module = "Site",
                        Key = key,
                        Value = value,
                        CreateTime = DateTime.UtcNow
                    };
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
