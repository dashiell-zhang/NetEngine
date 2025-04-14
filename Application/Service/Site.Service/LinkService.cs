using Authorize.Interface;
using Common;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using Shared.Model;
using Site.Interface;
using Site.Model.Article;
using Site.Model.Link;

namespace Site.Service
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class LinkService(DatabaseContext db, IdService idService, IUserContext userContext) : ILinkService
    {

        private long UserId => userContext.UserId;


        public async Task<DtoPageList<DtoLink>> GetLinkListAsync(DtoPageRequest request)
        {
            DtoPageList<DtoLink> result = new();

            var query = db.TLink.AsQueryable();

            result.Total = await query.CountAsync();

            result.List = await query.OrderByDescending(t => t.CreateTime).Select(t => new DtoLink
            {
                Id = t.Id,
                Name = t.Name,
                Url = t.Url,
                Sort = t.Sort,
                CreateTime = t.CreateTime
            }).Skip(request.Skip()).Take(request.PageSize).ToListAsync();


            return result;
        }


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
