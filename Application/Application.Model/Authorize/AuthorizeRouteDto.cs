namespace Application.Model.Authorize;

/// <summary>
/// 宿主提供的鉴权路由信息
/// </summary>
public class AuthorizeRouteDto
{

    /// <summary>
    /// 路由模板
    /// </summary>
    public string Route { get; set; } = string.Empty;


    /// <summary>
    /// 接口说明
    /// </summary>
    public string? Remark { get; set; }

}
