using Site.Model.Site;

namespace Site.Interface
{
    public interface ISiteService
    {


        /// <summary>
        /// 获取站点信息
        /// </summary>
        /// <returns></returns>
        Task<DtoSite> GetSiteAsync();


        /// <summary>
        /// 编辑站点信息
        /// </summary>
        /// <param name="editSite"></param>
        /// <returns></returns>
        Task<bool> EditSiteAsync(DtoSite editSite);



        /// <summary>
        /// 设置站点信息
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        Task<bool> SetSiteInfoAsync(string key, string? value);
    }
}
