﻿using AdminAPI.Services;
using AdminShared.Models;
using AdminShared.Models.Article;
using Common;
using IdentifierGenerator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Database;
using WebAPIBasic.Filters;
using WebAPIBasic.Libraries;

namespace AdminAPI.Controllers
{
    [SignVerifyFilter]
    [Route("[controller]/[action]")]
    [Authorize]
    [ApiController]
    public class ArticleController(DatabaseContext db, IConfiguration configuration, IdService idService, ArticleService articleService) : ControllerBase
    {
        private long userId => User.GetClaim<long>("userId");



        /// <summary>
        /// 获取栏目列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpGet]
        public DtoPageList<DtoCategory> GetCategoryList([FromQuery] DtoPageRequest request)
        {
            DtoPageList<DtoCategory> data = new();

            var query = db.TCategory.AsQueryable();

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
            }).Skip(request.Skip()).Take(request.PageSize).ToList();

            return data;
        }



        /// <summary>
        /// 获取栏目树形列表
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public List<DtoKeyValueChild> GetCategoryTreeList()
        {
            var list = db.TCategory.Where(t => t.ParentId == null).Select(t => new DtoKeyValueChild
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
        [HttpGet]
        public DtoCategory? GetCategory(long categoryId)
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
            }).FirstOrDefault();

            return category;
        }




        /// <summary>
        /// 创建栏目
        /// </summary>
        /// <param name="createCategory"></param>
        /// <returns></returns>
        [HttpPost]
        public long CreateCategory(DtoEditCategory createCategory)
        {
            TCategory category = new()
            {
                Id = idService.GetId(),
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
        [HttpPost]
        public bool UpdateCategory(long categoryId, DtoEditCategory updateCategory)
        {
            var category = db.TCategory.Where(t => t.Id == categoryId).FirstOrDefault();

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
        [HttpDelete]
        public bool DeleteCategory(long id)
        {
            var category = db.TCategory.Where(t => t.Id == id).FirstOrDefault();

            if (category != null)
            {
                category.IsDelete = true;
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
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpGet]
        public DtoPageList<DtoArticle> GetArticleList([FromQuery] DtoPageRequest request)
        {
            DtoPageList<DtoArticle> data = new();

            var query = db.TArticle.AsQueryable();

            data.Total = query.Count();

            string fileServerUrl = configuration["FileServerUrl"]?.ToString() ?? "";

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
                CoverImageList = db.TFile.Where(f => f.Sign == "cover" && f.Table == "TArticle" && f.TableId == t.Id).Select(f => new DtoKeyValue
                {
                    Key = f.Id,
                    Value = fileServerUrl + f.Path
                }).ToList()
            }).Skip(request.Skip()).Take(request.PageSize).ToList();

            return data;
        }





        /// <summary>
        /// 通过文章ID 获取文章信息
        /// </summary>
        /// <param name="articleId">文章ID</param>
        /// <returns></returns>
        [HttpGet]
        public DtoArticle? GetArticle(long articleId)
        {
            string fileServerUrl = configuration["FileServerUrl"]?.ToString() ?? "";

            var article = db.TArticle.Where(t => t.Id == articleId).Select(t => new DtoArticle
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
                CoverImageList = db.TFile.Where(f => f.Sign == "cover" && f.Table == "TArticle" && f.TableId == t.Id).Select(f => new DtoKeyValue
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
        [HttpPost]
        public long CreateArticle(DtoEditArticle createArticle, long fileKey)
        {
            TArticle article = new()
            {
                Id = idService.GetId(),
                CreateUserId = userId,
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


            var fileList = db.TFile.Where(t => t.Table == "TArticle" && t.TableId == fileKey).ToList();

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
        [HttpPost]
        public bool UpdateArticle(long articleId, DtoEditArticle updateArticle)
        {
            var article = db.TArticle.Where(t => t.Id == articleId).FirstOrDefault();

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
        [HttpDelete]
        public bool DeleteArticle(long id)
        {
            var article = db.TArticle.Where(t => t.Id == id).FirstOrDefault();

            if (article != null)
            {
                article.IsDelete = true;
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
