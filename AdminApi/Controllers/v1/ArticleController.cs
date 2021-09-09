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
        /// <param name="channelId">用户ID</param>
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
        public Guid CreateUser(dtoCreateChannel createChannel)
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
            var user = db.TChannel.Where(t => t.IsDelete == false & t.Id == id.Id).FirstOrDefault();

            user.IsDelete = true;
            user.DeleteTime = DateTime.Now;
            user.DeleteUserId = userId;

            db.SaveChanges();

            return true;
        }
    }
}
