using Application.Interface;
using Application.Model.Shared;
using Application.Model.Site.Article;
using Application.Service.Basic;
using Common;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using SourceGenerator.Runtime.Attributes;

namespace Application.Service.Site;
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class ArticleService(IUserContext userContext, DatabaseContext db, IdService idService, FileService fileService)
{

    private long UserId => userContext.UserId;


    /// <summary>
    /// 获取栏目列表
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<DtoPageList<DtoCategory>> GetCategoryListAsync(DtoPageRequest request)
    {

        DtoPageList<DtoCategory> result = new();

        var query = db.Category.AsQueryable();

        result.Total = await query.CountAsync();

        if (result.Total != 0)
        {
            result.List = await query.OrderByDescending(t => t.Id).Select(t => new DtoCategory
            {
                Id = t.Id,
                Name = t.Name,
                Remarks = t.Remarks,
                Sort = t.Sort,
                ParentId = t.ParentId,
                ParentName = t.Parent!.Name,
                CreateTime = t.CreateTime
            }).Skip(request.Skip()).Take(request.PageSize).ToListAsync();
        }

        return result;
    }


    /// <summary>
    /// 获取栏目选择列表
    /// </summary>
    /// <param name="id">类型Id</param>
    /// <returns></returns>
    public async Task<List<DtoCategorySelect>> GetCategorySelectListAsync(long? id = null)
    {
        var list = await db.Category.Where(t => t.ParentId == id).OrderBy(t => t.Sort).ThenBy(t => t.Id).Select(t => new DtoCategorySelect
        {
            Id = t.Id,
            Name = t.Name
        }).ToListAsync();

        foreach (var item in list)
        {
            item.ChildList = await GetCategorySelectListAsync(item.Id);
        }

        return list;
    }


    /// <summary>
    /// 通过栏目Id 获取栏目信息 
    /// </summary>
    /// <param name="categoryId">栏目ID</param>
    /// <returns></returns>
    public Task<DtoCategory?> GetCategoryAsync(long categoryId)
    {
        var category = db.Category.Where(t => t.Id == categoryId).Select(t => new DtoCategory
        {
            Id = t.Id,
            Name = t.Name,
            Remarks = t.Remarks,
            Sort = t.Sort,
            ParentId = t.ParentId,
            ParentName = t.Parent!.Name,
            CreateTime = t.CreateTime
        }).FirstOrDefaultAsync();

        return category;
    }


    /// <summary>
    /// 创建栏目
    /// </summary>
    /// <param name="createCategory"></param>
    /// <returns></returns>
    public async Task<long> CreateCategoryAsync(DtoEditCategory createCategory)
    {
        Category category = new()
        {
            Id = idService.GetId(),
            CreateUserId = UserId,
            Name = createCategory.Name,
            ParentId = createCategory.ParentId,
            Remarks = createCategory.Remarks,
            Sort = createCategory.Sort
        };

        db.Category.Add(category);

        await db.SaveChangesAsync();

        return category.Id;
    }


    /// <summary>
    /// 更新栏目信息
    /// </summary>
    /// <param name="categoryId"></param>
    /// <param name="updateCategory"></param>
    /// <returns></returns>
    public async Task<bool> UpdateCategoryAsync(long categoryId, DtoEditCategory updateCategory)
    {
        var category = await db.Category.Where(t => t.Id == categoryId).FirstOrDefaultAsync();

        if (category != null)
        {
            category.Name = updateCategory.Name;
            category.ParentId = updateCategory.ParentId;
            category.Remarks = updateCategory.Remarks;
            category.Sort = updateCategory.Sort;

            await db.SaveChangesAsync();

            return true;
        }

        throw new CustomException("无效的 categoryId");

    }


    /// <summary>
    /// 删除栏目
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task<bool> DeleteCategoryAsync(long id)
    {
        var category = await db.Category.Where(t => t.Id == id).FirstOrDefaultAsync();

        if (category != null)
        {
            category.IsDelete = true;
            category.DeleteUserId = UserId;

            await db.SaveChangesAsync();

            return true;
        }
        else
        {
            return false;
        }
    }


    /// <summary>
    /// 获取文章列表
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<DtoPageList<DtoArticle>> GetArticleListAsync(DtoPageRequest request)
    {
        DtoPageList<DtoArticle> result = new();

        var query = db.Article.AsQueryable();

        result.Total = await query.CountAsync();

        if (result.Total != 0)
        {
            result.List = await query.OrderByDescending(t => t.Id).Select(t => new DtoArticle
            {
                Id = t.Id,
                CategoryId = t.CategoryId,
                CategoryName = t.Category.Name,
                Title = t.Title,
                Content = t.Content,
                Digest = t.Digest,
                IsRecommend = t.IsRecommend,
                IsDisplay = t.IsDisplay,
                Sort = t.Sort,
                ClickCount = t.ClickCount,
                CreateTime = t.CreateTime,
            }).Skip(request.Skip()).Take(request.PageSize).ToListAsync();

            foreach (var article in result.List)
            {
                article.CoverImageList = await fileService.GetFileListAsync("Article", "cover", article.Id, true);
            }
        }

        return result;
    }


    /// <summary>
    /// 通过文章ID 获取文章信息
    /// </summary>
    /// <param name="articleId">文章ID</param>
    /// <returns></returns>
    public async Task<DtoArticle?> GetArticleAsync(long articleId)
    {
        var article = await db.Article.Where(t => t.Id == articleId).Select(t => new DtoArticle
        {
            Id = t.Id,
            CategoryId = t.CategoryId,
            CategoryName = t.Category.Name,
            Title = t.Title,
            Content = t.Content,
            Digest = t.Digest,
            IsRecommend = t.IsRecommend,
            IsDisplay = t.IsDisplay,
            Sort = t.Sort,
            ClickCount = t.ClickCount,
            CreateTime = t.CreateTime,
        }).FirstOrDefaultAsync();

        if (article != null)
        {
            article.CoverImageList = await fileService.GetFileListAsync("Article", "cover", article.Id, true);
        }

        return article;
    }


    /// <summary>
    /// 创建文章
    /// </summary>
    /// <param name="createArticle"></param>
    /// <param name="fileKey">文件key</param>
    /// <returns></returns>
    public async Task<long> CreateArticleAsync(DtoEditArticle createArticle, long fileKey)
    {
        Article article = new()
        {
            Id = idService.GetId(),
            CreateUserId = UserId,
            Title = createArticle.Title,
            Content = createArticle.Content,
            CategoryId = long.Parse(createArticle.CategoryId),
            IsRecommend = createArticle.IsRecommend,
            IsDisplay = createArticle.IsDisplay,
            Sort = createArticle.Sort,
            ClickCount = createArticle.ClickCount
        };

        if (string.IsNullOrEmpty(createArticle.Digest) && !string.IsNullOrEmpty(createArticle.Content))
        {
            string content = StringHelper.RemoveHtml(createArticle.Content);
            article.Digest = content.Length > 255 ? content[..255] : content;
        }
        else
        {
            article.Digest = createArticle.Digest!;
        }

        db.Article.Add(article);


        var fileList = await db.File.Where(t => t.Table == "Article" && t.TableId == fileKey).ToListAsync();

        foreach (var file in fileList)
        {
            file.TableId = article.Id;
        }

        await db.SaveChangesAsync();

        return article.Id;
    }


    /// <summary>
    /// 更新文章信息
    /// </summary>
    /// <param name="articleId"></param>
    /// <param name="updateArticle"></param>
    /// <returns></returns>
    public async Task<bool> UpdateArticleAsync(long articleId, DtoEditArticle updateArticle)
    {
        var article = await db.Article.Where(t => t.Id == articleId).FirstOrDefaultAsync();

        if (article != null)
        {
            article.CategoryId = long.Parse(updateArticle.CategoryId);
            article.Title = updateArticle.Title;
            article.Content = updateArticle.Content;
            article.IsRecommend = updateArticle.IsRecommend;
            article.IsDisplay = updateArticle.IsDisplay;
            article.Sort = updateArticle.Sort;
            article.ClickCount = updateArticle.ClickCount;

            if (string.IsNullOrEmpty(updateArticle.Digest) && !string.IsNullOrEmpty(updateArticle.Content))
            {
                string content = StringHelper.RemoveHtml(updateArticle.Content);
                article.Digest = content.Length > 255 ? content[..255] : content;
            }
            else
            {
                article.Digest = updateArticle.Digest;
            }

            await db.SaveChangesAsync();

            return true;
        }

        throw new CustomException("无效的 articleId");
    }


    /// <summary>
    /// 删除文章
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task<bool> DeleteArticleAsync(long id)
    {
        var article = await db.Article.Where(t => t.Id == id).FirstOrDefaultAsync();

        if (article != null)
        {
            article.IsDelete = true;
            article.DeleteUserId = UserId;

            await db.SaveChangesAsync();
        }

        return true;
    }

}
