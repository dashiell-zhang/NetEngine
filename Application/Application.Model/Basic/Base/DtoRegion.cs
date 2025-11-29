namespace Application.Model.Basic.Base;

/// <summary>
/// 区域信息模型
/// </summary>
public class DtoRegion
{

    /// <summary>
    /// 标识Id
    /// </summary>
    public long Id { get; set; }


    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; }


    /// <summary>
    /// 下级区域集合
    /// </summary>
    public List<DtoRegion>? ChildList { get; set; }

}
