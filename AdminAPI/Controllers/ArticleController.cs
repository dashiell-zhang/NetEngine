using AdminAPI.Filters;
using AdminAPI.Libraries;
using AdminAPI.Services;
using AdminShared.Models;
using AdminShared.Models.Article;
using Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Database;

namespace AdminAPI.Controllers
{
    [SignVerifyFilter]
    [Route("[controller]")]
    [Authorize]
    [ApiController]
    public class ArticleController : ControllerBase
    {


        private readonly DatabaseContext db;
        private readonly IConfiguration configuration;
        private readonly IDHelper idHelper;

        private readonly ArticleService articleService;

        private readonly long userId;



        public ArticleController(DatabaseContext db, IConfiguration configuration, IDHelper idHelper, IHttpContextAccessor httpContextAccessor, ArticleService articleService)
        {
            this.db = db;
            this.configuration = configuration;
            this.idHelper = idHelper;

            var userIdStr = httpContextAccessor.HttpContext?.GetClaimByAuthorization("userId");

            if (userIdStr != null)
            {
                userId = long.Parse(userIdStr);
            }

            this.articleService = articleService;
        }




        /// <summary>
        /// 获取栏目列表
        /// </summary>
        /// <param name="pageNum">页码</param>
        /// <param name="pageSize">单页数量</param>
        /// <param name="searchKey">搜索关键词</param>
        /// <returns></returns>
        [HttpGet("GetCategoryList")]
        public DtoPageList<DtoCategory> GetCategoryList(int pageNum, int pageSize, string? searchKey)
        {
            DtoPageList<DtoCategory> data = new();

            int skip = (pageNum - 1) * pageSize;

            var query = db.TCategory.Where(t => t.IsDelete == false);

            if (!string.IsNullOrEmpty(searchKey))
            {
                query = query.Where(t => t.Name.Contains(searchKey));
            }

            data.Total = query.Count();

            data.List = query.OrderByDescending(t => t.CreateTime).Select(t => new DtoCategory
            {
                Id = t.Id,
                Name = t.Name,
                Remarks = t.Remarks,
                Sort = t.Sort,
                ParentId = t.ParentId,
                ParentName = t.Parent!.Name,
                CreateTime = t.CreateTime
            }).Skip(skip).Take(pageSize).ToList();

            return data;
        }



        /// <summary>
        /// 获取栏目树形列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetCategoryTreeList")]
        public List<DtoKeyValueChild> GetCategoryTreeList()
        {
            var list = db.TCategory.Where(t => t.IsDelete == false && t.ParentId == null).Select(t => new DtoKeyValueChild
            {
                Key = t.Id,
                Value = t.Name
            }).ToList();

            foreach (var item in list)
            {
                item.ChildList = articleService.GetCategoryChildList(Convert.ToInt64(item.Key));
            }

            return list;
        }



        /// <summary>
        /// 通过栏目Id 获取栏目信息 
        /// </summary>
        /// <param name="categoryId">栏目ID</param>
        /// <returns></returns>
        [HttpGet("GetCategory")]
        public DtoCategory? GetCategory(long categoryId)
        {
            var category = db.TCategory.Where(t => t.IsDelete == false && t.Id == categoryId).Select(t => new DtoCategory
            {
                Id = t.Id,
                Name = t.Name,
                Remarks = t.Remarks,
                Sort = t.Sort,
                ParentId = t.ParentId,
                ParentName = t.Parent!.Name,
                CreateTime = t.CreateTime
            }).FirstOrDefault();

            return category;
        }




        /// <summary>
        /// 创建栏目
        /// </summary>
        /// <param name="createCategory"></param>
        /// <returns></returns>
        [HttpPost("CreateCategory")]
        public long CreateCategory(DtoEditCategory createCategory)
        {
            TCategory category = new()
            {
                Id = idHelper.GetId(),
                CreateTime = DateTime.UtcNow,
                CreateUserId = userId,
                Name = createCategory.Name,
                ParentId = createCategory.ParentId,
                Remarks = createCategory.Remarks,
                Sort = createCategory.Sort
            };

            db.TCategory.Add(category);

            db.SaveChanges();

            return category.Id;
        }




        /// <summary>
        /// 更新栏目信息
        /// </summary>
        /// <param name="categoryId"></param>
        /// <param name="updateCategory"></param>
        /// <returns></returns>
        [HttpPost("UpdateCategory")]
        public bool UpdateCategory(long categoryId, DtoEditCategory updateCategory)
        {
            var category = db.TCategory.Where(t => t.IsDelete == false && t.Id == categoryId).FirstOrDefault();

            if (category != null)
            {
                category.Name = updateCategory.Name;
                category.ParentId = updateCategory.ParentId;
                category.Remarks = updateCategory.Remarks;
                category.Sort = updateCategory.Sort;

                db.SaveChanges();

                return true;
            }
            else
            {
                return false;
            }
        }



        /// <summary>
        /// 删除栏目
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("DeleteCategory")]
        public bool DeleteCategory(long id)
        {
            var category = db.TCategory.Where(t => t.IsDelete == false && t.Id == id).FirstOrDefault();

            if (category != null)
            {
                category.IsDelete = true;
                category.DeleteTime = DateTime.UtcNow;
                category.DeleteUserId = userId;

                db.SaveChanges();

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
        /// <param name="pageNum">页码</param>
        /// <param name="pageSize">单页数量</param>
        /// <param name="searchKey">搜索关键词</param>
        /// <returns></returns>
        [HttpGet("GetArticleList")]
        public DtoPageList<DtoArticle> GetArticleList(int pageNum, int pageSize, string? searchKey)
        {
            DtoPageList<DtoArticle> data = new();

            int skip = (pageNum - 1) * pageSize;

            var query = db.TArticle.Where(t => t.IsDelete == false);

            if (!string.IsNullOrEmpty(searchKey))
            {
                query = query.Where(t => t.Title.Contains(searchKey));
            }

            data.Total = query.Count();

            var fileServerUrl = configuration["FileServerUrl"].ToString();

            data.List = query.OrderByDescending(t => t.CreateTime).Select(t => new DtoArticle
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
                CoverImageList = db.TFile.Where(f => f.IsDelete == false && f.Sign == "cover" && f.Table == "TArticle" && f.TableId == t.Id).Select(f => new DtoKeyValue
                {
                    Key = f.Id,
                    Value = fileServerUrl + f.Path
                }).ToList()
            }).Skip(skip).Take(pageSize).ToList();

            return data;
        }





        /// <summary>
        /// 通过文章ID 获取文章信息
        /// </summary>
        /// <param name="articleId">文章ID</param>
        /// <returns></returns>
        [HttpGet("GetArticle")]
        public DtoArticle? GetArticle(long articleId)
        {
            var fileServerUrl = configuration["FileServerUrl"].ToString();


            var article = db.TArticle.Where(t => t.IsDelete == false && t.Id == articleId).Select(t => new DtoArticle
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
                CoverImageList = db.TFile.Where(f => f.IsDelete == false && f.Sign == "cover" && f.Table == "TArticle" && f.TableId == t.Id).Select(f => new DtoKeyValue
                {
                    Key = f.Id,
                    Value = fileServerUrl + f.Path
                }).ToList()
            }).FirstOrDefault();

            return article;
        }




        /// <summary>
        /// 创建文章
        /// </summary>
        /// <param name="createArticle"></param>
        /// <param name="fileKey">文件key</param>
        /// <returns></returns>
        [HttpPost("CreateArticle")]
        public long CreateArticle(DtoEditArticle createArticle, long fileKey)
        {
            TArticle article = new()
            {
                Id = idHelper.GetId(),
                CreateTime = DateTime.UtcNow,
                CreateUserId = userId,
                Title = createArticle.Title,
                Content = createArticle.Content,
                CategoryId = createArticle.CategoryId,
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


            var fileList = db.TFile.Where(t => t.IsDelete == false && t.Table == "TArticle" && t.TableId == fileKey).ToList();

            foreach (var file in fileList)
            {
                file.TableId = article.Id;
            }

            db.SaveChanges();

            return article.Id;
        }




        /// <summary>
        /// 更新文章信息
        /// </summary>
        /// <param name="articleId"></param>
        /// <param name="updateArticle"></param>
        /// <returns></returns>
        [HttpPost("UpdateArticle")]
        public bool UpdateArticle(long articleId, DtoEditArticle updateArticle)
        {
            var article = db.TArticle.Where(t => t.IsDelete == false && t.Id == articleId).FirstOrDefault();

            if (article != null)
            {
                article.CategoryId = updateArticle.CategoryId;
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

                db.SaveChanges();

                return true;
            }
            else
            {
                return false;
            }
        }



        /// <summary>
        /// 删除文章
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("DeleteArticle")]
        public bool DeleteArticle(long id)
        {
            var article = db.TArticle.Where(t => t.IsDelete == false && t.Id == id).FirstOrDefault();

            if (article != null)
            {
                article.IsDelete = true;
                article.DeleteTime = DateTime.UtcNow;
                article.DeleteUserId = userId;

                db.SaveChanges();

                return true;
            }
            else
            {
                return false;
            }

        }


    }
}
