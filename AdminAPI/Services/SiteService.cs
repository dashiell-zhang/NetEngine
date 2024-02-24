using Common;
using IdentifierGenerator;
using Repository.Database;

namespace AdminAPI.Services
{

    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class SiteService(DatabaseContext db, IdService idService)
    {
        public bool SetSiteInfo(string key, string? value)
        {

            if (value != null)
            {

                var appSetting = db.TAppSetting.Where(t => t.Module == "Site" && t.Key == key).FirstOrDefault();

                if (appSetting == null)
                {
                    appSetting = new()
                    {
                        Id = idService.GetId(),
                        Module = "Site",
                        Key = key,
                        Value = value
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
