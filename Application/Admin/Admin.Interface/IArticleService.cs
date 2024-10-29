using Admin.Model.Article;
using Shared.Model;

namespace Admin.Interface
{
    public interface IArticleService
    {

        /// <summary>
        /// 获取栏目列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public DtoPageList<DtoCategory> GetCategoryList(DtoPageRequest request);



        /// <summary>
        /// 获取栏目树形列表
        /// </summary>
        /// <returns></returns>
        public List<DtoKeyValueChild> GetCategoryTreeList();



        /// <summary>
        /// 通过栏目Id 获取栏目信息 
        /// </summary>
        /// <param name="categoryId">栏目ID</param>
        /// <returns></returns>
        public DtoCategory? GetCategory(long categoryId);



        /// <summary>
        /// 创建栏目
        /// </summary>
        /// <param name="createCategory"></param>
        /// <returns></returns>
        public long CreateCategory(DtoEditCategory createCategory);



        /// <summary>
        /// 更新栏目信息
        /// </summary>
        /// <param name="categoryId"></param>
        /// <param name="updateCategory"></param>
        /// <returns></returns>
        public bool UpdateCategory(long categoryId, DtoEditCategory updateCategory);



        /// <summary>
        /// 删除栏目
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool DeleteCategory(long id);



        /// <summary>
        /// 获取文章列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public DtoPageList<DtoArticle> GetArticleList(DtoPageRequest request);



        /// <summary>
        /// 通过文章ID 获取文章信息
        /// </summary>
        /// <param name="articleId">文章ID</param>
        /// <returns></returns>
        public DtoArticle? GetArticle(long articleId);



        /// <summary>
        /// 创建文章
        /// </summary>
        /// <param name="createArticle"></param>
        /// <param name="fileKey">文件key</param>
        /// <returns></returns>
        public long CreateArticle(DtoEditArticle createArticle, long fileKey);



        /// <summary>
        /// 更新文章信息
        /// </summary>
        /// <param name="articleId"></param>
        /// <param name="updateArticle"></param>
        /// <returns></returns>
        public bool UpdateArticle(long articleId, DtoEditArticle updateArticle);



        /// <summary>
        /// 删除文章
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool DeleteArticle(long id);



        /// <summary>
        /// 获取栏目下所有子级列表
        /// </summary>
        /// <param name="categoryId"></param>
        /// <returns></returns>
        public List<DtoKeyValueChild>? GetCategoryChildList(long categoryId);

    }
}
