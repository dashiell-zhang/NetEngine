using Application.Model.Authorize;

namespace Application.Interface;

/// <summary>
/// 维护权限路由及权限基础数据
/// </summary>
public interface IAuthorizeMaintenanceService
{

    /// <summary>
    /// 同步指定宿主需要鉴权的路由
    /// </summary>
    Task UpdateRoutesAsync(string module, IReadOnlyCollection<AuthorizeRouteDto> routes);


    /// <summary>
    /// 在事务内初始化管理员、角色与权限基础数据
    /// </summary>
    Task<AuthorizeInitResultDto> InitializeAsync(AuthorizeSeedData seedData);

}
