using AdminApi.Libraries;
using AdminApi.Models.v1.Article;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.Dtos;
using Repository.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        public Guid CreateChannel(dtoCreateChannel createChannel)
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
        /// <param name="updateChannel"></param>
        /// <returns></returns>
        [HttpPost("UpdateChannel")]
        public bool UpdateChannel(dtoUpdateChannel updateChannel)
        {
            var channel = db.TChannel.Where(t => t.IsDelete == false & t.Id == updateChannel.Id).FirstOrDefault();

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
        /// 通过栏目Id 获取栏目信息 
        /// </summary>
        /// <param name="categoryId">栏目ID</param>
        /// <returns></returns>
        [HttpGet("GetCategory")]
        public dtoChannel GetCategory(Guid categoryId)
        {
            var channel = db.TCategory.Where(t => t.IsDelete == false & t.Id == categoryId).Select(t => new dtoChannel
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
        /// 创建栏目
        /// </summary>
        /// <param name="createCategory"></param>
        /// <returns></returns>
        [HttpPost("CreateCategory")]
        public Guid CreateCategory(dtoCreateCategory createCategory)
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
        /// <param name="updateCategory"></param>
        /// <returns></returns>
        [HttpPost("UpdateCategory")]
        public bool UpdateCategory(dtoUpdateCategory updateCategory)
        {
            var category = db.TCategory.Where(t => t.IsDelete == false & t.Id == updateCategory.Id).FirstOrDefault();

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
    }
}
