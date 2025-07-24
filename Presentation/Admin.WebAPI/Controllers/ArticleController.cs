using Application.Interface.Site;
using Application.Model.Shared;
using Application.Model.Site.Article;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Core.Filters;

namespace Admin.WebAPI.Controllers
{
    [SignVerifyFilter]
    [Route("[controller]/[action]")]
    [Authorize]
    [ApiController]
    public class ArticleController(IArticleService articleService) : ControllerBase
    {


        /// <summary>
        /// 获取栏目列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpGet]
        public Task<DtoPageList<DtoCategory>> GetCategoryList([FromQuery] DtoPageRequest request) => articleService.GetCategoryListAsync(request);



        /// <summary>
        /// 获取栏目树形列表
        /// </summary>
        /// <param name="id">栏目Id</param>
        /// <returns></returns>
        [HttpGet]
        public Task<List<DtoCategorySelect>> GetCategorySelectList(long? id = null) => articleService.GetCategorySelectListAsync(id);



        /// <summary>
        /// 通过栏目Id 获取栏目信息 
        /// </summary>
        /// <param name="categoryId">栏目ID</param>
        /// <returns></returns>
        [HttpGet]
        public Task<DtoCategory?> GetCategory(long categoryId) => articleService.GetCategoryAsync(categoryId);



        /// <summary>
        /// 创建栏目
        /// </summary>
        /// <param name="createCategory"></param>
        /// <returns></returns>
        [HttpPost]
        public Task<long> CreateCategory(DtoEditCategory createCategory) => articleService.CreateCategoryAsync(createCategory);



        /// <summary>
        /// 更新栏目信息
        /// </summary>
        /// <param name="categoryId"></param>
        /// <param name="updateCategory"></param>
        /// <returns></returns>
        [HttpPost]
        public Task<bool> UpdateCategory(long categoryId, DtoEditCategory updateCategory) => articleService.UpdateCategoryAsync(categoryId, updateCategory);



        /// <summary>
        /// 删除栏目
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        public Task<bool> DeleteCategory(long id) => articleService.DeleteCategoryAsync(id);



        /// <summary>
        /// 获取文章列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpGet]
        public Task<DtoPageList<DtoArticle>> GetArticleList([FromQuery] DtoPageRequest request) => articleService.GetArticleListAsync(request);



        /// <summary>
        /// 通过文章ID 获取文章信息
        /// </summary>
        /// <param name="articleId">文章ID</param>
        /// <returns></returns>
        [HttpGet]
        public Task<DtoArticle?> GetArticle(long articleId) => articleService.GetArticleAsync(articleId);



        /// <summary>
        /// 创建文章
        /// </summary>
        /// <param name="createArticle"></param>
        /// <param name="fileKey">文件key</param>
        /// <returns></returns>
        [HttpPost]
        public Task<long> CreateArticle(DtoEditArticle createArticle, long fileKey) => articleService.CreateArticleAsync(createArticle, fileKey);



        /// <summary>
        /// 更新文章信息
        /// </summary>
        /// <param name="articleId"></param>
        /// <param name="updateArticle"></param>
        /// <returns></returns>
        [HttpPost]
        public Task<bool> UpdateArticle(long articleId, DtoEditArticle updateArticle) => articleService.UpdateArticleAsync(articleId, updateArticle);



        /// <summary>
        /// 删除文章
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        public Task<bool> DeleteArticle(long id) => articleService.DeleteArticleAsync(id);


    }
}
