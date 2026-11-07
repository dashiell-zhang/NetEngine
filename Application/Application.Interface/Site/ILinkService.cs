using Application.Model.Shared;
using Application.Model.Site.Link;

namespace Application.Interface.Site;

/// <summary>
/// 友情链接管理
/// </summary>
public interface ILinkService
{

    /// <summary>
    /// 获取友情链接列表
    /// </summary>
    Task<PageListDto<LinkDto>> GetLinkListAsync(PageRequestDto request);


    /// <summary>
    /// 获取友情链接
    /// </summary>
    Task<LinkDto?> GetLinkAsync(long linkId);


    /// <summary>
    /// 创建友情链接
    /// </summary>
    Task<long> CreateLinkAsync(long actorUserId, EditLinkDto createLink);


    /// <summary>
    /// 更新友情链接
    /// </summary>
    Task<bool> UpdateLinkAsync(long linkId, EditLinkDto updateLink);


    /// <summary>
    /// 删除友情链接
    /// </summary>
    Task<bool> DeleteLinkAsync(long actorUserId, long id);

}
