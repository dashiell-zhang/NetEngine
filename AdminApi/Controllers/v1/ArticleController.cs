using AdminApi.Filters;
using AdminShared.Models;
using AdminShared.Models.v1.Article;
using Common;
using Common.DistributedLock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Database;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdminApi.Controllers.v1
{
    [SignVerifyFilter]
    [ApiVersion("1")]
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class ArticleController : ControllerBase
    {

        private readonly long userId;

        private readonly DatabaseContext db;
        private readonly IDistributedLock distLock;
        private readonly SnowflakeHelper snowflakeHelper;



        public ArticleController(DatabaseContext db, IDistributedLock distLock, SnowflakeHelper snowflakeHelper)
        {
            this.db = db;
            this.distLock = distLock;
            this.snowflakeHelper = snowflakeHelper;

            var userIdStr = Libraries.Verify.JWTToken.GetClaims("userId");

            if (userIdStr != null)
            {
                userId = long.Parse(userIdStr);
            }
        }



        /// <summary>
        /// 获取频道列表
        /// </summary>
        /// <param name="pageNum">页码</param>
        /// <param name="pageSize">单页数量</param>
        /// <param name="searchKey">搜索关键词</param>
        /// <returns></returns>
        [HttpGet("GetChannelList")]
        public DtoPageList<DtoChannel> GetChannelList(int pageNum, int pageSize, string? searchKey)
        {
            var data = new DtoPageList<DtoChannel>();

            int skip = (pageNum - 1) * pageSize;

            var query = db.TChannel.Where(t => t.IsDelete == false);

            if (!string.IsNullOrEmpty(searchKey))
            {
                query = query.Where(t => t.Name.Contains(searchKey));
            }

            data.Total = query.Count();

            data.List = query.OrderByDescending(t => t.CreateTime).Select(t => new DtoChannel
            {
                Id = t.Id,
                Name = t.Name,
                Remarks = t.Remarks,
                Sort = t.Sort,
                CreateTime = t.CreateTime
            }).Skip(skip).Take(pageSize).ToList();

            return data;
        }



        /// <summary>
        /// 获取频道KV列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetChannelKVList")]
        public List<DtoKeyValue> GetChannelKVList()
        {
            var list = db.TChannel.Where(t => t.IsDelete == false).OrderBy(t => t.Sort).ThenBy(t => t.CreateTime).Select(t => new DtoKeyValue
            {
                Key = t.Id,
                Value = t.Name
            }).ToList();

            return list;
        }



        /// <summary>
        /// 通过频道Id 获取频道信息 
        /// </summary>
        /// <param name="channelId">频道ID</param>
        /// <returns></returns>
        [HttpGet("GetChannel")]
        public DtoChannel? GetChannel(long channelId)
        {
            var channel = db.TChannel.Where(t => t.IsDelete == false && t.Id == channelId).Select(t => new DtoChannel
            {
                Id = t.Id,
                Name = t.Name,
                Remarks = t.Remarks,
                Sort = t.Sort,
                CreateTime = t.CreateTime
            }).FirstOrDefault();

            return channel;
        }




        /// <summary>
        /// 创建频道
        /// </summary>
        /// <param name="createChannel"></param>
        /// <returns></returns>
        [HttpPost("CreateChannel")]
        public long CreateChannel(DtoEditChannel createChannel)
        {
            TChannel channel = new();
            channel.Id = snowflakeHelper.GetId();
            channel.Name = createChannel.Name;
            channel.CreateTime = DateTime.UtcNow;
            channel.CreateUserId = userId;

            channel.Remarks = createChannel.Remarks;
            channel.Sort = createChannel.Sort;

            db.TChannel.Add(channel);

            db.SaveChanges();

            return channel.Id;
        }




        /// <summary>
        /// 更新频道信息
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="updateChannel"></param>
        /// <returns></returns>
        [HttpPost("UpdateChannel")]
        public bool UpdateChannel(long channelId, DtoEditChannel updateChannel)
        {
            var channel = db.TChannel.Where(t => t.IsDelete == false && t.Id == channelId).FirstOrDefault();

            if (channel != null)
            {
                channel.Name = updateChannel.Name;
                channel.Remarks = updateChannel.Remarks;
                channel.Sort = updateChannel.Sort;

                db.SaveChanges();

                return true;
            }
            else
            {
                return false;
            }

        }



        /// <summary>
        /// 删除频道
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("DeleteChannel")]
        public bool DeleteChannel(long id)
        {
            var channel = db.TChannel.Where(t => t.IsDelete == false && t.Id == id).FirstOrDefault();

            if (channel != null)
            {
                channel.IsDelete = true;
                channel.DeleteTime = DateTime.UtcNow;
                channel.DeleteUserId = userId;

                db.SaveChanges();

                return true;
            }
            else
            {
                return false;
            }
        }




        /// <summary>
        /// 获取栏目列表
        /// </summary>
        /// <param name="channelId">频道ID</param>
        /// <param name="pageNum">页码</param>
        /// <param name="pageSize">单页数量</param>
        /// <param name="searchKey">搜索关键词</param>
        /// <returns></returns>
        [HttpGet("GetCategoryList")]
        public DtoPageList<DtoCategory> GetCategoryList(long channelId, int pageNum, int pageSize, string? searchKey)
        {
            var data = new DtoPageList<DtoCategory>();

            int skip = (pageNum - 1) * pageSize;

            var query = db.TCategory.Where(t => t.IsDelete == false && t.ChannelId == channelId);

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
        /// 获取栏目KV列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetCategoryKVList")]
        public List<DtoKeyValue> GetCategoryKVList(long channelId)
        {
            var list = db.TCategory.Where(t => t.IsDelete == false && t.ChannelId == channelId).OrderBy(t => t.Sort).ThenBy(t => t.CreateTime).Select(t => new DtoKeyValue
            {
                Key = t.Id,
                Value = t.Name
            }).ToList();

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
            TCategory category = new();
            category.Id = snowflakeHelper.GetId();
            category.CreateTime = DateTime.UtcNow;
            category.CreateUserId = userId;
            category.Name = createCategory.Name;
            category.ChannelId = createCategory.ChannelId;
            category.ParentId = createCategory.ParentId;
            category.Remarks = createCategory.Remarks;
            category.Sort = createCategory.Sort;

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
        /// <param name="channelId">频道ID</param>
        /// <param name="pageNum">页码</param>
        /// <param name="pageSize">单页数量</param>
        /// <param name="searchKey">搜索关键词</param>
        /// <returns></returns>
        [HttpGet("GetArticleList")]
        public DtoPageList<DtoArticle> GetArticleList(long channelId, int pageNum, int pageSize, string? searchKey)
        {
            var data = new DtoPageList<DtoArticle>();

            int skip = (pageNum - 1) * pageSize;

            var query = db.TArticle.Where(t => t.IsDelete == false && t.Category.ChannelId == channelId);

            if (!string.IsNullOrEmpty(searchKey))
            {
                query = query.Where(t => t.Title.Contains(searchKey));
            }

            data.Total = query.Count();

            var fileServerUrl = Common.IOHelper.GetConfig()["FileServerUrl"].ToString();

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
            var fileServerUrl = Common.IOHelper.GetConfig()["FileServerUrl"].ToString();


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
            TArticle article = new();
            article.Id = snowflakeHelper.GetId();
            article.CreateTime = DateTime.UtcNow;
            article.CreateUserId = userId;
            article.Title = createArticle.Title;
            article.Content = createArticle.Content;
            article.CategoryId = createArticle.CategoryId;
            article.IsRecommend = createArticle.IsRecommend;
            article.IsDisplay = createArticle.IsDisplay;
            article.Sort = createArticle.Sort;
            article.ClickCount = createArticle.ClickCount;

            if (string.IsNullOrEmpty(createArticle.Digest) && !string.IsNullOrEmpty(createArticle.Content))
            {
                string content = Common.StringHelper.RemoveHtml(createArticle.Content);
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
                    string content = Common.StringHelper.RemoveHtml(updateArticle.Content);
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
