using Shared.Model;
using Site.Model.Link;

namespace Site.Interface
{
    public interface ILinkService
    {

        /// <summary>
        /// 获取友情链接列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        Task<DtoPageList<DtoLink>> GetLinkListAsync(DtoPageRequest request);


        /// <summary>
        /// 获取友情链接
        /// </summary>
        /// <param name="linkId">链接ID</param>
        /// <returns></returns>
        Task<DtoLink?> GetLinkAsync(long linkId);


        /// <summary>
        /// 创建友情链接
        /// </summary>
        /// <param name="createLink"></param>
        /// <returns></returns>
        Task<long> CreateLinkAsync(DtoEditLink createLink);


        /// <summary>
        /// 更新友情链接
        /// </summary>
        /// <param name="linkId"></param>
        /// <param name="updateLink"></param>
        /// <returns></returns>
        Task<bool> UpdateLinkAsync(long linkId, DtoEditLink updateLink);


        /// <summary>
        /// 删除友情链接
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<bool> DeleteLinkAsync(long id);

    }
}
