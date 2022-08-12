using Common;
using Repository.Database;

namespace AdminAPI.Services
{

    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class SiteService
    {


        private readonly DatabaseContext db;
        private readonly IDHelper idHelper;

        public SiteService(DatabaseContext db, IDHelper idHelper)
        {
            this.db = db;
            this.idHelper = idHelper;
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
                        Id = idHelper.GetId(),
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
