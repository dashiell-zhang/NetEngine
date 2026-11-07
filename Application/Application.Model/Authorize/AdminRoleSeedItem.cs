namespace Application.Model.Authorize;

/// <summary>
/// 管理员角色初始化配置
/// </summary>
public class AdminRoleSeedItem
{

    /// <summary>
    /// 角色编码
    /// </summary>
    public string Code { get; set; } = string.Empty;


    /// <summary>
    /// 显示名称
    /// </summary>
    public string Name { get; set; } = string.Empty;


    /// <summary>
    /// 说明
    /// </summary>
    public string? Remark { get; set; }

}
