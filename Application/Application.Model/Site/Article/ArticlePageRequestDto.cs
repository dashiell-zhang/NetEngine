using Application.Model.Shared;

namespace Application.Model.Site.Article;

/// <summary>
/// 文章分页请求入参
/// </summary>
public class ArticlePageRequestDto : PageRequestDto
{

    /// <summary>
    /// 频道栏目ID
    /// </summary>
    public long? ChannelId { get; set; }

}
