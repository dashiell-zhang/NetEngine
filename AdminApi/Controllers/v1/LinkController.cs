using AdminApi.Libraries;
using AdminShared.Models;
using AdminShared.Models.v1.Link;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Database;
using System;
using System.Linq;

namespace AdminApi.Controllers.v1
{
    [ApiVersion("1")]
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class LinkController : ControllerCore
    {



        /// <summary>
        /// 获取友情链接列表
        /// </summary>
        /// <param name="pageNum">页码</param>
        /// <param name="pageSize">单页数量</param>
        /// <param name="searchKey">搜索关键词</param>
        /// <returns></returns>
        [HttpGet("GetLinkList")]
        public DtoPageList<DtoLink> GetLinkList(int pageNum, int pageSize, string? searchKey)
        {
            var data = new DtoPageList<DtoLink>();

            int skip = (pageNum - 1) * pageSize;

            var query = db.TLink.Where(t => t.IsDelete == false);

            if (!string.IsNullOrEmpty(searchKey))
            {
                query = query.Where(t => t.Name.Contains(searchKey));
            }

            data.Total = query.Count();

            data.List = query.OrderByDescending(t => t.CreateTime).Select(t => new DtoLink
            {
                Id = t.Id,
                Name = t.Name,
                Url = t.Url,
                Sort = t.Sort,
                CreateTime = t.CreateTime
            }).Skip(skip).Take(pageSize).ToList();

            return data;
        }



        /// <summary>
        /// 获取友情链接
        /// </summary>
        /// <param name="linkId">链接ID</param>
        /// <returns></returns>
        [HttpGet("GetLink")]
        public DtoLink? GetLink(long linkId)
        {
            var link = db.TLink.Where(t => t.IsDelete == false && t.Id == linkId).Select(t => new DtoLink
            {
                Id = t.Id,
                Name = t.Name,
                Url = t.Url,
                Sort = t.Sort,
                CreateTime = t.CreateTime
            }).FirstOrDefault();

            return link;
        }




        /// <summary>
        /// 创建友情链接
        /// </summary>
        /// <param name="createLink"></param>
        /// <returns></returns>
        [HttpPost("CreateLink")]
        public long CreateLink(DtoEditLink createLink)
        {
            TLink link = new(createLink.Name, createLink.Url);
            link.Id = snowflakeHelper.GetId();
            link.CreateTime = DateTime.UtcNow;
            link.CreateUserId = userId;

            link.Sort = createLink.Sort;

            db.TLink.Add(link);

            db.SaveChanges();

            return link.Id;
        }




        /// <summary>
        /// 更新友情链接
        /// </summary>
        /// <param name="linkId"></param>
        /// <param name="updateLink"></param>
        /// <returns></returns>
        [HttpPost("UpdateLink")]
        public bool UpdateLink(long linkId, DtoEditLink updateLink)
        {
            var link = db.TLink.Where(t => t.IsDelete == false && t.Id == linkId).FirstOrDefault();

            if (link != null)
            {
                link.Name = updateLink.Name;
                link.Url = updateLink.Url;
                link.Sort = updateLink.Sort;

                db.SaveChanges();
            }

            return true;
        }



        /// <summary>
        /// 删除友情链接
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("DeleteLink")]
        public bool DeleteLink(DtoId id)
        {
            var link = db.TLink.Where(t => t.IsDelete == false && t.Id == id.Id).FirstOrDefault();

            if (link != null)
            {
                link.IsDelete = true;
                link.DeleteTime = DateTime.UtcNow;
                link.DeleteUserId = userId;

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
