namespace Application.Model.Authorize;

/// <summary>
/// 权限基础数据初始化参数
/// </summary>
public class AuthorizeSeedData
{

    /// <summary>
    /// 管理员账号配置
    /// </summary>
    public AdminUserSeedItem AdminUser { get; set; } = new();


    /// <summary>
    /// 管理员角色配置
    /// </summary>
    public AdminRoleSeedItem AdminRole { get; set; } = new();


    /// <summary>
    /// 功能配置列表
    /// </summary>
    public List<FunctionSeedItem> Functions { get; set; } = [];


    /// <summary>
    /// 路由映射配置列表
    /// </summary>
    public List<FunctionRouteSeedItem> FunctionRoutes { get; set; } = [];

}
