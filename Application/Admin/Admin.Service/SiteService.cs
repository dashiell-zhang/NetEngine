using Admin.Interface;
using Admin.Model.Site;
using Common;
using IdentifierGenerator;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;

namespace Admin.Service
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class SiteService(DatabaseContext db, IdService idService) : ISiteService
    {


        public DtoSite GetSite()
        {
            var kvList = db.TAppSetting.Where(t => t.Module == "Site").Select(t => new
            {
                t.Key,
                t.Value
            }).ToList();

            DtoSite site = new()
            {
                WebUrl = kvList.Where(t => t.Key == "WebUrl").Select(t => t.Value).FirstOrDefault(),
                ManagerName = kvList.Where(t => t.Key == "ManagerName").Select(t => t.Value).FirstOrDefault(),
                ManagerAddress = kvList.Where(t => t.Key == "ManagerAddress").Select(t => t.Value).FirstOrDefault(),
                ManagerPhone = kvList.Where(t => t.Key == "ManagerPhone").Select(t => t.Value).FirstOrDefault(),
                ManagerEmail = kvList.Where(t => t.Key == "ManagerEmail").Select(t => t.Value).FirstOrDefault(),
                RecordNumber = kvList.Where(t => t.Key == "RecordNumber").Select(t => t.Value).FirstOrDefault(),
                SeoTitle = kvList.Where(t => t.Key == "SeoTitle").Select(t => t.Value).FirstOrDefault(),
                SeoKeyWords = kvList.Where(t => t.Key == "SeoKeyWords").Select(t => t.Value).FirstOrDefault(),
                SeoDescription = kvList.Where(t => t.Key == "SeoDescription").Select(t => t.Value).FirstOrDefault(),
                FootCode = kvList.Where(t => t.Key == "FootCode").Select(t => t.Value).FirstOrDefault()
            };

            return site;
        }



        public bool EditSite(DtoSite editSite)
        {
            var query = db.TAppSetting.Where(t => t.Module == "Site");

            SetSiteInfo("WebUrl", editSite.WebUrl);
            SetSiteInfo("ManagerName", editSite.ManagerName);
            SetSiteInfo("ManagerAddress", editSite.ManagerAddress);
            SetSiteInfo("ManagerPhone", editSite.ManagerPhone);
            SetSiteInfo("ManagerEmail", editSite.ManagerEmail);
            SetSiteInfo("RecordNumber", editSite.RecordNumber);
            SetSiteInfo("SeoTitle", editSite.SeoTitle);
            SetSiteInfo("SeoKeyWords", editSite.SeoKeyWords);
            SetSiteInfo("SeoDescription", editSite.SeoDescription);
            SetSiteInfo("FootCode", editSite.FootCode);

            return true;
        }



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
