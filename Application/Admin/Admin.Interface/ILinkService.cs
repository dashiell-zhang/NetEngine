using Admin.Model.Link;
using Shared.Model;

namespace Admin.Interface
{
    public interface ILinkService
    {


        /// <summary>
        /// 获取友情链接列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public DtoPageList<DtoLink> GetLinkList(DtoPageRequest request);



        /// <summary>
        /// 获取友情链接
        /// </summary>
        /// <param name="linkId">链接ID</param>
        /// <returns></returns>
        public DtoLink? GetLink(long linkId);



        /// <summary>
        /// 创建友情链接
        /// </summary>
        /// <param name="createLink"></param>
        /// <returns></returns>
        public long CreateLink(DtoEditLink createLink);



        /// <summary>
        /// 更新友情链接
        /// </summary>
        /// <param name="linkId"></param>
        /// <param name="updateLink"></param>
        /// <returns></returns>
        public bool UpdateLink(long linkId, DtoEditLink updateLink);



        /// <summary>
        /// 删除友情链接
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool DeleteLink(long id);
    }
}
