namespace Application.Model.Authorize;

/// <summary>
/// 权限基础数据初始化结果
/// </summary>
public class AuthorizeInitResultDto
{

    /// <summary>
    /// 管理员登录名
    /// </summary>
    public string AdminUser { get; set; } = string.Empty;


    /// <summary>
    /// 管理员角色编码
    /// </summary>
    public string AdminRole { get; set; } = string.Empty;


    /// <summary>
    /// 初始化的功能数量
    /// </summary>
    public int FunctionCount { get; set; }


    /// <summary>
    /// 初始化的路由映射数量
    /// </summary>
    public int FunctionRouteCount { get; set; }


    /// <summary>
    /// 管理员角色的功能授权数量
    /// </summary>
    public int FunctionAuthorizeCount { get; set; }


    /// <summary>
    /// 管理员绑定的角色数量
    /// </summary>
    public int UserRoleCount { get; set; }

}
