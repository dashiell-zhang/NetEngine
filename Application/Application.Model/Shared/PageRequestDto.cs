using System.ComponentModel.DataAnnotations;

namespace Application.Model.Shared;

/// <summary>
/// 分页请求基本入参
/// </summary>
public class PageRequestDto : IValidatableObject
{


    /// <summary>
    /// 页码
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;



    /// <summary>
    /// 单页数量
    /// </summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;



    /// <summary>
    /// 获取跳过数量
    /// </summary>
    /// <returns></returns>
    public int Skip()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(Page, 1);
        return checked((int)Offset);
    }

    private long Offset => ((long)Page - 1) * PageSize;

    /// <summary>
    /// 分页偏移必须在数据访问层支持的 Int32 范围内。
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Offset > int.MaxValue)
        {
            yield return new ValidationResult($"分页偏移不可以大于 {int.MaxValue}", [nameof(Page)]);
        }
    }

}
