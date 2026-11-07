namespace Application.Model.Authorize;

/// <summary>
/// 管理员初始化配置
/// </summary>
public class AdminUserSeedItem
{

    /// <summary>
    /// 显示名称
    /// </summary>
    public string Name { get; set; } = string.Empty;


    /// <summary>
    /// 登录名
    /// </summary>
    public string UserName { get; set; } = string.Empty;


    /// <summary>
    /// 联系电话
    /// </summary>
    public string Phone { get; set; } = string.Empty;


    /// <summary>
    /// 电子邮箱
    /// </summary>
    public string? Email { get; set; }


    /// <summary>
    /// 初始化密码
    /// </summary>
    public string Password { get; set; } = string.Empty;

}
