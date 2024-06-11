using AdminShared.Models;
using AdminShared.Models.Link;
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
    public class LinkController : ControllerBase
    {

        private readonly DatabaseContext db;
        private readonly IdService idService;

        private readonly long userId;



        public LinkController(DatabaseContext db, IdService idService, IHttpContextAccessor httpContextAccessor)
        {
            this.db = db;
            this.idService = idService;

            var userIdStr = httpContextAccessor.HttpContext?.GetClaimByUser("userId");
            if (userIdStr != null)
            {
                userId = long.Parse(userIdStr);
            }
        }



        /// <summary>
        /// 获取友情链接列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpGet]
        public DtoPageList<DtoLink> GetLinkList([FromQuery] DtoPageRequest request)
        {
            DtoPageList<DtoLink> data = new();

            var query = db.TLink.AsQueryable();

            data.Total = query.Count();

            data.List = query.OrderByDescending(t => t.CreateTime).Select(t => new DtoLink
            {
                Id = t.Id,
                Name = t.Name,
                Url = t.Url,
                Sort = t.Sort,
                CreateTime = t.CreateTime
            }).Skip(request.Skip()).Take(request.PageSize).ToList();

            return data;
        }



        /// <summary>
        /// 获取友情链接
        /// </summary>
        /// <param name="linkId">链接ID</param>
        /// <returns></returns>
        [HttpGet]
        public DtoLink? GetLink(long linkId)
        {
            var link = db.TLink.Where(t => t.Id == linkId).Select(t => new DtoLink
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
        [HttpPost]
        public long CreateLink(DtoEditLink createLink)
        {
            TLink link = new()
            {
                Id = idService.GetId(),
                Name = createLink.Name,
                Url = createLink.Url,
                CreateUserId = userId,
                Sort = createLink.Sort
            };

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
        [HttpPost]
        public bool UpdateLink(long linkId, DtoEditLink updateLink)
        {
            var link = db.TLink.Where(t => t.Id == linkId).FirstOrDefault();

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
        [HttpDelete]
        public bool DeleteLink(long id)
        {
            var link = db.TLink.Where(t => t.Id == id).FirstOrDefault();

            if (link != null)
            {
                link.IsDelete = true;
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
