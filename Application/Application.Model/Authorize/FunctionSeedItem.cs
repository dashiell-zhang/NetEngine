namespace Application.Model.Authorize;

/// <summary>
/// 功能初始化配置
/// </summary>
public class FunctionSeedItem
{

    /// <summary>
    /// 显示名称
    /// </summary>
    public string Name { get; set; } = string.Empty;


    /// <summary>
    /// 功能标记
    /// </summary>
    public string Sign { get; set; } = string.Empty;


    /// <summary>
    /// 说明
    /// </summary>
    public string? Remark { get; set; }


    /// <summary>
    /// 父功能标记
    /// </summary>
    public string? ParentSign { get; set; }


    /// <summary>
    /// 功能类型
    /// </summary>
    public int Type { get; set; }

}
