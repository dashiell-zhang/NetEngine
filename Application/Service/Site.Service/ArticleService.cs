using Authorize.Interface;
using Basic.Interface;
using Common;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using Shared.Model;
using Site.Interface;
using Site.Model.Article;

namespace Site.Service
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class ArticleService(IUserContext userContext, DatabaseContext db, IdService idService, IFileService fileService) : IArticleService
    {

        private long UserId => userContext.UserId;


        public async Task<DtoPageList<DtoCategory>> GetCategoryListAsync(DtoPageRequest request)
        {

            DtoPageList<DtoCategory> result = new();

            var query = db.TCategory.AsQueryable();

            result.Total = await query.CountAsync();

            result.List = await query.OrderByDescending(t => t.CreateTime).Select(t => new DtoCategory
            {
                Id = t.Id,
                Name = t.Name,
                Remarks = t.Remarks,
                Sort = t.Sort,
                ParentId = t.ParentId,
                ParentName = t.Parent!.Name,
                CreateTime = t.CreateTime
            }).Skip(request.Skip()).Take(request.PageSize).ToListAsync();


            return result;
        }


        public async Task<List<DtoCategorySelect>> GetCategorySelectListAsync(long? id = null)
        {
            var list = await db.TCategory.Where(t => t.ParentId == id).OrderBy(t => t.Sort).ThenBy(t => t.Id).Select(t => new DtoCategorySelect
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


        public Task<DtoCategory?> GetCategoryAsync(long categoryId)
        {
            var category = db.TCategory.Where(t => t.Id == categoryId).Select(t => new DtoCategory
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


        public async Task<long> CreateCategoryAsync(DtoEditCategory createCategory)
        {
            TCategory category = new()
            {
                Id = idService.GetId(),
                CreateUserId = UserId,
                Name = createCategory.Name,
                ParentId = createCategory.ParentId,
                Remarks = createCategory.Remarks,
                Sort = createCategory.Sort
            };

            db.TCategory.Add(category);

            await db.SaveChangesAsync();

            return category.Id;
        }


        public async Task<bool> UpdateCategoryAsync(long categoryId, DtoEditCategory updateCategory)
        {
            var category = await db.TCategory.Where(t => t.Id == categoryId).FirstOrDefaultAsync();

            if (category != null)
            {
                category.Name = updateCategory.Name;
                category.ParentId = updateCategory.ParentId;
                category.Remarks = updateCategory.Remarks;
                category.Sort = updateCategory.Sort;

                await db.SaveChangesAsync();

                return true;
            }
            else
            {
                return false;
            }
        }


        public async Task<bool> DeleteCategoryAsync(long id)
        {
            var category = await db.TCategory.Where(t => t.Id == id).FirstOrDefaultAsync();

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


        public async Task<DtoPageList<DtoArticle>> GetArticleListAsync(DtoPageRequest request)
        {
            DtoPageList<DtoArticle> result = new();

            var query = db.TArticle.AsQueryable();

            result.Total = await query.CountAsync();

            result.List = await query.OrderByDescending(t => t.CreateTime).Select(t => new DtoArticle
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

            return result;
        }


        public async Task<DtoArticle?> GetArticleAsync(long articleId)
        {
            var article = await db.TArticle.Where(t => t.Id == articleId).Select(t => new DtoArticle
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


        public async Task<long> CreateArticleAsync(DtoEditArticle createArticle, long fileKey)
        {
            TArticle article = new()
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

            db.TArticle.Add(article);


            var fileList = await db.TFile.Where(t => t.Table == "TArticle" && t.TableId == fileKey).ToListAsync();

            foreach (var file in fileList)
            {
                file.TableId = article.Id;
            }

            await db.SaveChangesAsync();

            return article.Id;
        }


        public async Task<bool> UpdateArticleAsync(long articleId, DtoEditArticle updateArticle)
        {
            var article = await db.TArticle.Where(t => t.Id == articleId).FirstOrDefaultAsync();

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
            else
            {
                return false;
            }
        }


        public async Task<bool> DeleteArticleAsync(long id)
        {
            var article = await db.TArticle.Where(t => t.Id == id).FirstOrDefaultAsync();

            if (article != null)
            {
                article.IsDelete = true;
                article.DeleteUserId = UserId;

                await db.SaveChangesAsync();

                return true;
            }
            else
            {
                return false;
            }

        }

    }
}
