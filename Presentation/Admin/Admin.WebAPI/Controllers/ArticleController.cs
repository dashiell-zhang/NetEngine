using Admin.Interface;
using Admin.Model;
using Admin.Model.Article;
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
        public DtoPageList<DtoCategory> GetCategoryList([FromQuery] DtoPageRequest request)
        {
            return articleService.GetCategoryList(request);
        }



        /// <summary>
        /// 获取栏目树形列表
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public List<DtoKeyValueChild> GetCategoryTreeList()
        {
            return articleService.GetCategoryTreeList();
        }



        /// <summary>
        /// 通过栏目Id 获取栏目信息 
        /// </summary>
        /// <param name="categoryId">栏目ID</param>
        /// <returns></returns>
        [HttpGet]
        public DtoCategory? GetCategory(long categoryId)
        {
            return articleService.GetCategory(categoryId);
        }




        /// <summary>
        /// 创建栏目
        /// </summary>
        /// <param name="createCategory"></param>
        /// <returns></returns>
        [HttpPost]
        public long CreateCategory(DtoEditCategory createCategory)
        {
            return articleService.CreateCategory(createCategory);
        }




        /// <summary>
        /// 更新栏目信息
        /// </summary>
        /// <param name="categoryId"></param>
        /// <param name="updateCategory"></param>
        /// <returns></returns>
        [HttpPost]
        public bool UpdateCategory(long categoryId, DtoEditCategory updateCategory)
        {
            return articleService.UpdateCategory(categoryId, updateCategory);
        }



        /// <summary>
        /// 删除栏目
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        public bool DeleteCategory(long id)
        {
            return articleService.DeleteCategory(id);
        }




        /// <summary>
        /// 获取文章列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpGet]
        public DtoPageList<DtoArticle> GetArticleList([FromQuery] DtoPageRequest request)
        {
            return articleService.GetArticleList(request);
        }





        /// <summary>
        /// 通过文章ID 获取文章信息
        /// </summary>
        /// <param name="articleId">文章ID</param>
        /// <returns></returns>
        [HttpGet]
        public DtoArticle? GetArticle(long articleId)
        {
            return articleService.GetArticle(articleId);
        }




        /// <summary>
        /// 创建文章
        /// </summary>
        /// <param name="createArticle"></param>
        /// <param name="fileKey">文件key</param>
        /// <returns></returns>
        [HttpPost]
        public long CreateArticle(DtoEditArticle createArticle, long fileKey)
        {
            return articleService.CreateArticle(createArticle, fileKey);
        }




        /// <summary>
        /// 更新文章信息
        /// </summary>
        /// <param name="articleId"></param>
        /// <param name="updateArticle"></param>
        /// <returns></returns>
        [HttpPost]
        public bool UpdateArticle(long articleId, DtoEditArticle updateArticle)
        {
            return articleService.UpdateArticle(articleId, updateArticle);
        }



        /// <summary>
        /// 删除文章
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        public bool DeleteArticle(long id)
        {
            return articleService.DeleteArticle(id);
        }


    }
}
