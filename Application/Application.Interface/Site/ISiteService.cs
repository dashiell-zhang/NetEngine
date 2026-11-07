using Application.Model.Site.Site;

namespace Application.Interface.Site;

/// <summary>
/// 站点信息查询与编辑
/// </summary>
public interface ISiteService
{

    /// <summary>
    /// 获取站点信息
    /// </summary>
    Task<SiteDto> GetSiteAsync();


    /// <summary>
    /// 编辑站点信息
    /// </summary>
    Task<bool> EditSiteAsync(SiteDto editSite);

}
