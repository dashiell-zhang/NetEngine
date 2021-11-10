using AdminApi.Libraries;
using AdminShared.Models;
using AdminShared.Models.v1.Article;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Database;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdminApi.Controllers.v1
{
    [ApiVersion("1")]
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class ArticleController : ControllerCore
    {



        /// <summary>
        /// 获取频道列表
        /// </summary>
        /// <param name="pageNum">页码</param>
        /// <param name="pageSize">单页数量</param>
        /// <param name="searchKey">搜索关键词</param>
        /// <returns></returns>
        [HttpGet("GetChannelList")]
        public dtoPageList<dtoChannel> GetChannelList(int pageNum, int pageSize, string searchKey)
        {
            var data = new dtoPageList<dtoChannel>();

            int skip = (pageNum - 1) * pageSize;

            var query = db.TChannel.Where(t => t.IsDelete == false);

            if (!string.IsNullOrEmpty(searchKey))
            {
                query = query.Where(t => t.Name.Contains(searchKey));
            }

            data.Total = query.Count();

            data.List = query.OrderByDescending(t => t.CreateTime).Select(t => new dtoChannel
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
        public List<dtoKeyValue> GetChannelKVList()
        {
            var list = db.TChannel.Where(t => t.IsDelete == false).OrderBy(t => t.Sort).ThenBy(t => t.CreateTime).Select(t => new dtoKeyValue
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
        public dtoChannel GetChannel(Guid channelId)
        {
            var channel = db.TChannel.Where(t => t.IsDelete == false & t.Id == channelId).Select(t => new dtoChannel
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
        public Guid CreateChannel(dtoEditChannel createChannel)
        {
            var channel = new TChannel();
            channel.Id = Guid.NewGuid();
            channel.CreateTime = DateTime.Now;
            channel.CreateUserId = userId;

            channel.Name = createChannel.Name;
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
        public bool UpdateChannel(Guid channelId, dtoEditChannel updateChannel)
        {
            var channel = db.TChannel.Where(t => t.IsDelete == false & t.Id == channelId).FirstOrDefault();

            channel.Name = updateChannel.Name;
            channel.Remarks = updateChannel.Remarks;
            channel.Sort = updateChannel.Sort;

            db.SaveChanges();

            return true;
        }



        /// <summary>
        /// 删除频道
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("DeleteChannel")]
        public bool DeleteChannel(dtoId id)
        {
            var channel = db.TChannel.Where(t => t.IsDelete == false & t.Id == id.Id).FirstOrDefault();

            channel.IsDelete = true;
            channel.DeleteTime = DateTime.Now;
            channel.DeleteUserId = userId;

            db.SaveChanges();

            return true;
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
        public dtoPageList<dtoCategory> GetCategoryList(Guid channelId, int pageNum, int pageSize, string searchKey)
        {
            var data = new dtoPageList<dtoCategory>();

            int skip = (pageNum - 1) * pageSize;

            var query = db.TCategory.Where(t => t.IsDelete == false & t.ChannelId == channelId);

            if (!string.IsNullOrEmpty(searchKey))
            {
                query = query.Where(t => t.Name.Contains(searchKey));
            }

            data.Total = query.Count();

            data.List = query.OrderByDescending(t => t.CreateTime).Select(t => new dtoCategory
            {
                Id = t.Id,
                Name = t.Name,
                Remarks = t.Remarks,
                Sort = t.Sort,
                ParentId = t.ParentId,
                ParentName = t.Parent.Name,
                CreateTime = t.CreateTime
            }).Skip(skip).Take(pageSize).ToList();

            return data;
        }



        /// <summary>
        /// 获取栏目KV列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetCategoryKVList")]
        public List<dtoKeyValue> GetCategoryKVList(Guid channelId)
        {
            var list = db.TCategory.Where(t => t.IsDelete == false & t.ChannelId == channelId).OrderBy(t => t.Sort).ThenBy(t => t.CreateTime).Select(t => new dtoKeyValue
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
        public dtoCategory GetCategory(Guid categoryId)
        {
            var category = db.TCategory.Where(t => t.IsDelete == false & t.Id == categoryId).Select(t => new dtoCategory
            {
                Id = t.Id,
                Name = t.Name,
                Remarks = t.Remarks,
                Sort = t.Sort,
                ParentId = t.ParentId,
                ParentName = t.Parent.Name,
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
        public Guid CreateCategory(dtoEditCategory createCategory)
        {
            var category = new TCategory();
            category.Id = Guid.NewGuid();
            category.CreateTime = DateTime.Now;
            category.CreateUserId = userId;

            category.ChannelId = createCategory.ChannelId;
            category.Name = createCategory.Name;
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
        public bool UpdateCategory(Guid categoryId, dtoEditCategory updateCategory)
        {
            var category = db.TCategory.Where(t => t.IsDelete == false & t.Id == categoryId).FirstOrDefault();

            category.Name = updateCategory.Name;
            category.ParentId = updateCategory.ParentId;
            category.Remarks = updateCategory.Remarks;
            category.Sort = updateCategory.Sort;

            db.SaveChanges();

            return true;
        }



        /// <summary>
        /// 删除栏目
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("DeleteCategory")]
        public bool DeleteCategory(dtoId id)
        {
            var category = db.TCategory.Where(t => t.IsDelete == false & t.Id == id.Id).FirstOrDefault();

            category.IsDelete = true;
            category.DeleteTime = DateTime.Now;
            category.DeleteUserId = userId;

            db.SaveChanges();

            return true;
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
        public dtoPageList<dtoArticle> GetArticleList(Guid channelId, int pageNum, int pageSize, string searchKey)
        {
            var data = new dtoPageList<dtoArticle>();

            int skip = (pageNum - 1) * pageSize;

            var query = db.TArticle.Where(t => t.IsDelete == false & t.Category.ChannelId == channelId);

            if (!string.IsNullOrEmpty(searchKey))
            {
                query = query.Where(t => t.Title.Contains(searchKey));
            }

            data.Total = query.Count();

            data.List = query.OrderByDescending(t => t.CreateTime).Select(t => new dtoArticle
            {
                Id = t.Id,
                CategoryId = t.CategoryId,
                CategoryName = t.Category.Name,
                Title = t.Title,
                Content = t.Content,
                IsRecommend = t.IsRecommend,
                IsDisplay = t.IsDisplay,
                Sort = t.Sort,
                ClickCount = t.ClickCount,
                Abstract = t.Abstract,
                CreateTime = t.CreateTime,
                CoverImageList = db.TFile.Where(f => f.IsDelete == false && f.Sign == "cover" & f.Table == "TArticle" & f.TableId == t.Id).Select(f => new dtoKeyValue
                {
                    Key = f.Id,
                    Value = f.Path
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
        public dtoArticle GetArticle(Guid articleId)
        {
            var article = db.TArticle.Where(t => t.IsDelete == false & t.Id == articleId).Select(t => new dtoArticle
            {
                Id = t.Id,
                CategoryId = t.CategoryId,
                CategoryName = t.Category.Name,
                Title = t.Title,
                Content = t.Content,
                IsRecommend = t.IsRecommend,
                IsDisplay = t.IsDisplay,
                Sort = t.Sort,
                ClickCount = t.ClickCount,
                Abstract = t.Abstract,
                CreateTime = t.CreateTime,
                CoverImageList = db.TFile.Where(f => f.IsDelete == false && f.Sign == "cover" & f.Table == "TArticle" & f.TableId == t.Id).Select(f => new dtoKeyValue
                {
                    Key = f.Id,
                    Value = f.Path
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
        public Guid CreateArticle(dtoEditArticle createArticle, Guid fileKey)
        {
            var article = new TArticle();
            article.Id = Guid.NewGuid();
            article.CreateTime = DateTime.Now;
            article.CreateUserId = userId;

            article.CategoryId = createArticle.CategoryId;
            article.Title = createArticle.Title;
            article.Content = createArticle.Content;
            article.IsRecommend = createArticle.IsRecommend;
            article.IsDisplay = createArticle.IsDisplay;
            article.Sort = createArticle.Sort;
            article.ClickCount = createArticle.ClickCount;
            article.Abstract = createArticle.Abstract;

            db.TArticle.Add(article);


            var fileList = db.TFile.Where(t => t.IsDelete == false & t.Table == "TArticle" & t.TableId == fileKey).ToList();

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
        public bool UpdateArticle(Guid articleId, dtoEditArticle updateArticle)
        {
            var article = db.TArticle.Where(t => t.IsDelete == false & t.Id == articleId).FirstOrDefault();

            article.CategoryId = updateArticle.CategoryId;
            article.Title = updateArticle.Title;
            article.Content = updateArticle.Content;
            article.IsRecommend = updateArticle.IsRecommend;
            article.IsDisplay = updateArticle.IsDisplay;
            article.Sort = updateArticle.Sort;
            article.ClickCount = updateArticle.ClickCount;
            article.Abstract = updateArticle.Abstract;

            db.SaveChanges();

            return true;
        }



        /// <summary>
        /// 删除文章
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("DeleteArticle")]
        public bool DeleteArticle(dtoId id)
        {
            var article = db.TArticle.Where(t => t.IsDelete == false & t.Id == id.Id).FirstOrDefault();

            article.IsDelete = true;
            article.DeleteTime = DateTime.Now;
            article.DeleteUserId = userId;

            db.SaveChanges();

            return true;
        }


    }
}
