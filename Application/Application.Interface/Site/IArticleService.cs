using Application.Model.Shared;
using Application.Model.Site.Article;

namespace Application.Interface.Site
{
    public interface IArticleService
    {

        /// <summary>
        /// 获取栏目列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        Task<DtoPageList<DtoCategory>> GetCategoryListAsync(DtoPageRequest request);


        /// <summary>
        /// 获取栏目选择列表
        /// </summary>
        /// <param name="id">类型Id</param>
        /// <returns></returns>
        Task<List<DtoCategorySelect>> GetCategorySelectListAsync(long? id = null);


        /// <summary>
        /// 通过栏目Id 获取栏目信息 
        /// </summary>
        /// <param name="categoryId">栏目ID</param>
        /// <returns></returns>
        Task<DtoCategory?> GetCategoryAsync(long categoryId);


        /// <summary>
        /// 创建栏目
        /// </summary>
        /// <param name="createCategory"></param>
        /// <returns></returns>
        Task<long> CreateCategoryAsync(DtoEditCategory createCategory);


        /// <summary>
        /// 更新栏目信息
        /// </summary>
        /// <param name="categoryId"></param>
        /// <param name="updateCategory"></param>
        /// <returns></returns>
        Task<bool> UpdateCategoryAsync(long categoryId, DtoEditCategory updateCategory);


        /// <summary>
        /// 删除栏目
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<bool> DeleteCategoryAsync(long id);


        /// <summary>
        /// 获取文章列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        Task<DtoPageList<DtoArticle>> GetArticleListAsync(DtoPageRequest request);


        /// <summary>
        /// 通过文章ID 获取文章信息
        /// </summary>
        /// <param name="articleId">文章ID</param>
        /// <returns></returns>
        Task<DtoArticle?> GetArticleAsync(long articleId);


        /// <summary>
        /// 创建文章
        /// </summary>
        /// <param name="createArticle"></param>
        /// <param name="fileKey">文件key</param>
        /// <returns></returns>
        Task<long> CreateArticleAsync(DtoEditArticle createArticle, long fileKey);


        /// <summary>
        /// 更新文章信息
        /// </summary>
        /// <param name="articleId"></param>
        /// <param name="updateArticle"></param>
        /// <returns></returns>
        Task<bool> UpdateArticleAsync(long articleId, DtoEditArticle updateArticle);


        /// <summary>
        /// 删除文章
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<bool> DeleteArticleAsync(long id);

    }
}
