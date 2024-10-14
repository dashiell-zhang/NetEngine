using Admin.Model.Site;

namespace Admin.Interface
{
    public interface ISiteService
    {


        /// <summary>
        /// 获取站点信息
        /// </summary>
        /// <returns></returns>
        public DtoSite GetSite();


        /// <summary>
        /// 编辑站点信息
        /// </summary>
        /// <param name="editSite"></param>
        /// <returns></returns>
        public bool EditSite(DtoSite editSite);



        /// <summary>
        /// 设置站点信息
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetSiteInfo(string key, string? value);
    }
}
