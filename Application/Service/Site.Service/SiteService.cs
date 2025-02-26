using Common;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using Site.Interface;
using Site.Model.Site;

namespace Site.Service
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class SiteService(DatabaseContext db, IdService idService) : ISiteService
    {


        public async Task<DtoSite> GetSiteAsync()
        {
            var kvList = await db.TAppSetting.Where(t => t.Module == "Site").Select(t => new
            {
                t.Key,
                t.Value
            }).ToListAsync();

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



        public async Task<bool> EditSiteAsync(DtoSite editSite)
        {
            var tasks = new List<Task<bool>>
            {
                SetSiteInfoAsync("WebUrl", editSite.WebUrl),
                SetSiteInfoAsync("ManagerName", editSite.ManagerName),
                SetSiteInfoAsync("ManagerAddress", editSite.ManagerAddress),
                SetSiteInfoAsync("ManagerPhone", editSite.ManagerPhone),
                SetSiteInfoAsync("ManagerEmail", editSite.ManagerEmail),
                SetSiteInfoAsync("RecordNumber", editSite.RecordNumber),
                SetSiteInfoAsync("SeoTitle", editSite.SeoTitle),
                SetSiteInfoAsync("SeoKeyWords", editSite.SeoKeyWords),
                SetSiteInfoAsync("SeoDescription", editSite.SeoDescription),
                SetSiteInfoAsync("FootCode", editSite.FootCode)
             };

            await Task.WhenAll(tasks);
            return tasks.All(t => t.Result);
        }



        public async Task<bool> SetSiteInfoAsync(string key, string? value)
        {

            if (value != null)
            {

                var appSetting = await db.TAppSetting.Where(t => t.Module == "Site" && t.Key == key).FirstOrDefaultAsync();

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

                await db.SaveChangesAsync();
            }

            return true;
        }
    }
}
