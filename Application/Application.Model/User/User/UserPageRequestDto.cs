using Application.Model.Shared;

namespace Application.Model.User.User;

/// <summary>
/// 用户列表分页查询入参
/// </summary>
public class UserPageRequestDto : PageRequestDto
{

    /// <summary>
    /// 名称/用户名/电话/邮箱 模糊检索关键字（可选）
    /// </summary>
    public string? Keyword { get; set; }

}

