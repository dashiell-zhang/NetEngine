using Application.Interface;
using Application.Model.Shared;
using Application.Model.Site.Link;
using Common;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;

namespace Application.Service.Site
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class LinkService(DatabaseContext db, IdService idService, IUserContext userContext)
    {

        private long UserId => userContext.UserId;


        /// <summary>
        /// 获取友情链接列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<DtoPageList<DtoLink>> GetLinkListAsync(DtoPageRequest request)
        {
            DtoPageList<DtoLink> result = new();

            var query = db.TLink.AsQueryable();

            result.Total = await query.CountAsync();

            if (result.Total != 0)
            {
                result.List = await query.OrderByDescending(t => t.Id).Select(t => new DtoLink
                {
                    Id = t.Id,
                    Name = t.Name,
                    Url = t.Url,
                    Sort = t.Sort,
                    CreateTime = t.CreateTime
                }).Skip(request.Skip()).Take(request.PageSize).ToListAsync();
            }

            return result;
        }


        /// <summary>
        /// 获取友情链接
        /// </summary>
        /// <param name="linkId">链接ID</param>
        /// <returns></returns>
        public Task<DtoLink?> GetLinkAsync(long linkId)
        {
            var link = db.TLink.Where(t => t.Id == linkId).Select(t => new DtoLink
            {
                Id = t.Id,
                Name = t.Name,
                Url = t.Url,
                Sort = t.Sort,
                CreateTime = t.CreateTime
            }).FirstOrDefaultAsync();

            return link;
        }


        /// <summary>
        /// 创建友情链接
        /// </summary>
        /// <param name="createLink"></param>
        /// <returns></returns>
        public async Task<long> CreateLinkAsync(DtoEditLink createLink)
        {
            TLink link = new()
            {
                Id = idService.GetId(),
                Name = createLink.Name,
                Url = createLink.Url,
                CreateUserId = UserId,
                Sort = createLink.Sort
            };

            db.TLink.Add(link);

            await db.SaveChangesAsync();

            return link.Id;
        }


        /// <summary>
        /// 更新友情链接
        /// </summary>
        /// <param name="linkId"></param>
        /// <param name="updateLink"></param>
        /// <returns></returns>
        public async Task<bool> UpdateLinkAsync(long linkId, DtoEditLink updateLink)
        {
            var link = await db.TLink.Where(t => t.Id == linkId).FirstOrDefaultAsync();

            if (link != null)
            {
                link.Name = updateLink.Name;
                link.Url = updateLink.Url;
                link.Sort = updateLink.Sort;

                await db.SaveChangesAsync();

                return true;
            }

            throw new CustomException("无效的 linkId");

        }


        /// <summary>
        /// 删除友情链接
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<bool> DeleteLinkAsync(long id)
        {
            var link = await db.TLink.Where(t => t.Id == id).FirstOrDefaultAsync();

            if (link != null)
            {
                link.IsDelete = true;
                link.DeleteUserId = UserId;

                await db.SaveChangesAsync();

            }
            return true;
        }

    }
}
