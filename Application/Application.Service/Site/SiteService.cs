using Application.Model.Site.Site;
using Common;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository;
using SourceGenerator.Runtime.Attributes;

namespace Application.Service.Site;
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class SiteService(DatabaseContext db, IdService idService)
{

    /// <summary>
    /// 获取站点信息
    /// </summary>
    /// <returns></returns>
    public async Task<SiteDto> GetSiteAsync()
    {
        var kvList = await db.AppSetting.Where(t => t.Module == "Site").Select(t => new
        {
            t.Key,
            t.Value
        }).ToListAsync();

        SiteDto site = new()
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


    /// <summary>
    /// 编辑站点信息
    /// </summary>
    /// <param name="editSite"></param>
    /// <returns></returns>
    public async Task<bool> EditSiteAsync(SiteDto editSite)
    {
        await SetSiteInfoAsync("WebUrl", editSite.WebUrl);
        await SetSiteInfoAsync("ManagerName", editSite.ManagerName);
        await SetSiteInfoAsync("ManagerAddress", editSite.ManagerAddress);
        await SetSiteInfoAsync("ManagerPhone", editSite.ManagerPhone);
        await SetSiteInfoAsync("ManagerEmail", editSite.ManagerEmail);
        await SetSiteInfoAsync("RecordNumber", editSite.RecordNumber);
        await SetSiteInfoAsync("SeoTitle", editSite.SeoTitle);
        await SetSiteInfoAsync("SeoKeyWords", editSite.SeoKeyWords);
        await SetSiteInfoAsync("SeoDescription", editSite.SeoDescription);
        await SetSiteInfoAsync("FootCode", editSite.FootCode);

        return true;
    }


    /// <summary>
    /// 设置站点信息
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public async Task<bool> SetSiteInfoAsync(string key, string? value)
    {

        if (value != null)
        {

            var appSetting = await db.AppSetting.Where(t => t.Module == "Site" && t.Key == key).FirstOrDefaultAsync();

            if (appSetting == null)
            {
                appSetting = new()
                {
                    Id = idService.GetId(),
                    Module = "Site",
                    Key = key,
                    Value = value
                };
                db.AppSetting.Add(appSetting);
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
