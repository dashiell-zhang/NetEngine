using Application.Model.Shared;
using Application.Model.Site.Article;

namespace Application.Interface.Site;

/// <summary>
/// 文章与文章分类管理
/// </summary>
public interface IArticleService
{

    /// <summary>
    /// 获取栏目列表
    /// </summary>
    Task<PageListDto<CategoryDto>> GetCategoryListAsync(PageRequestDto request);


    /// <summary>
    /// 获取栏目选择列表
    /// </summary>
    Task<List<CategorySelectDto>> GetCategorySelectListAsync();


    /// <summary>
    /// 通过栏目Id 获取栏目信息 
    /// </summary>
    Task<CategoryDto?> GetCategoryAsync(long categoryId);


    /// <summary>
    /// 创建栏目
    /// </summary>
    Task<long> CreateCategoryAsync(long actorUserId, EditCategoryDto createCategory);


    /// <summary>
    /// 更新栏目信息
    /// </summary>
    Task<bool> UpdateCategoryAsync(long categoryId, EditCategoryDto updateCategory);


    /// <summary>
    /// 删除栏目
    /// </summary>
    Task<bool> DeleteCategoryAsync(long actorUserId, long id);


    /// <summary>
    /// 获取文章列表
    /// </summary>
    Task<PageListDto<ArticleDto>> GetArticleListAsync(PageRequestDto request);


    /// <summary>
    /// 通过文章ID 获取文章信息
    /// </summary>
    Task<ArticleDto?> GetArticleAsync(long articleId);


    /// <summary>
    /// 创建文章
    /// </summary>
    Task<long> CreateArticleAsync(long actorUserId, EditArticleDto createArticle, long uploadKey);


    /// <summary>
    /// 更新文章信息
    /// </summary>
    Task<bool> UpdateArticleAsync(long actorUserId, long articleId, EditArticleDto updateArticle, long uploadKey);


    /// <summary>
    /// 删除文章
    /// </summary>
    Task<bool> DeleteArticleAsync(long actorUserId, long id);

}
