using WebAPI.Core.Extensions;
using Application.Model.Shared;
using Application.Model.Site.Article;
using Application.Service.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Core.Filters;

namespace Admin.WebAPI.Controllers;

/// <summary>
/// 栏目和文章管理控制器
/// </summary>
[SignVerifyFilter]
[Route("[controller]/[action]")]
[Authorize]
[ApiController]
public class ArticleController(ArticleService articleService) : ControllerBase
{


    /// <summary>
    /// 获取栏目列表
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpGet]
    public Task<PageListDto<CategoryDto>> GetCategoryList([FromQuery] PageRequestDto request) => articleService.GetCategoryListAsync(request);



    /// <summary>
    /// 获取栏目树形列表
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public Task<List<CategorySelectDto>> GetCategorySelectList() => articleService.GetCategorySelectListAsync();



    /// <summary>
    /// 通过栏目Id 获取栏目信息 
    /// </summary>
    /// <param name="categoryId">栏目ID</param>
    /// <returns></returns>
    [HttpGet]
    public Task<CategoryDto?> GetCategory(long categoryId) => articleService.GetCategoryAsync(categoryId);



    /// <summary>
    /// 创建栏目
    /// </summary>
    /// <param name="createCategory"></param>
    /// <returns></returns>
    [HttpPost]
    public Task<long> CreateCategory(EditCategoryDto createCategory) => articleService.CreateCategoryAsync(User.GetUserId(), createCategory);



    /// <summary>
    /// 更新栏目信息
    /// </summary>
    /// <param name="categoryId"></param>
    /// <param name="updateCategory"></param>
    /// <returns></returns>
    [HttpPost]
    public Task<bool> UpdateCategory(long categoryId, EditCategoryDto updateCategory) => articleService.UpdateCategoryAsync(categoryId, updateCategory);



    /// <summary>
    /// 删除栏目
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpDelete]
    public Task<bool> DeleteCategory(long id) => articleService.DeleteCategoryAsync(User.GetUserId(), id);



    /// <summary>
    /// 获取文章列表
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpGet]
    public Task<PageListDto<ArticleDto>> GetArticleList([FromQuery] PageRequestDto request) => articleService.GetArticleListAsync(request);



    /// <summary>
    /// 通过文章ID 获取文章信息
    /// </summary>
    /// <param name="articleId">文章ID</param>
    /// <returns></returns>
    [HttpGet]
    public Task<ArticleDto?> GetArticle(long articleId) => articleService.GetArticleAsync(articleId);



    /// <summary>
    /// 创建文章
    /// </summary>
    /// <param name="createArticle"></param>
    /// <param name="uploadKey">上传批次标识</param>
    /// <returns></returns>
    [HttpPost]
    public Task<long> CreateArticle(EditArticleDto createArticle, long uploadKey) => articleService.CreateArticleAsync(User.GetUserId(), createArticle, uploadKey);



    /// <summary>
    /// 更新文章信息
    /// </summary>
    /// <param name="articleId"></param>
    /// <param name="updateArticle"></param>
    /// <param name="uploadKey">上传批次标识</param>
    /// <returns></returns>
    [HttpPost]
    public Task<bool> UpdateArticle(long articleId, EditArticleDto updateArticle, long uploadKey) => articleService.UpdateArticleAsync(User.GetUserId(), articleId, updateArticle, uploadKey);



    /// <summary>
    /// 删除文章
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpDelete]
    public Task<bool> DeleteArticle(long id) => articleService.DeleteArticleAsync(User.GetUserId(), id);


}
