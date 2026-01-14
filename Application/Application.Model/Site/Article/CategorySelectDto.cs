namespace Application.Model.Site.Article;
/// <summary>
/// 类型选择列表模型
/// </summary>
public class CategorySelectDto
{

    /// <summary>
    /// 标识Id
    /// </summary>
    public long Id { get; set; }


    /// <summary>
    /// 类型名称
    /// </summary>
    public string Name { get; set; }


    /// <summary>
    /// 
    /// </summary>
    public List<CategorySelectDto> ChildList { get; set; }
}
