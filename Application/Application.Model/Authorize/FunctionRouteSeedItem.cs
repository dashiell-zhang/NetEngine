namespace Application.Model.Authorize;

/// <summary>
/// 路由与功能映射初始化配置
/// </summary>
public class FunctionRouteSeedItem
{

    /// <summary>
    /// 所属宿主模块
    /// </summary>
    public string Module { get; set; } = string.Empty;


    /// <summary>
    /// 路由模板
    /// </summary>
    public string Route { get; set; } = string.Empty;


    /// <summary>
    /// 说明
    /// </summary>
    public string? Remark { get; set; }


    /// <summary>
    /// 关联功能标记
    /// </summary>
    public string? FunctionSign { get; set; }

}
