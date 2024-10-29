using Admin.Interface;
using Admin.Model.Link;
using Common;
using IdentifierGenerator;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using Shared.Interface;
using Shared.Model;

namespace Admin.Service
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class LinkService(DatabaseContext db, IdService idService, IUserContext userContext) : ILinkService
    {

        private long userId => userContext.UserId;



        public DtoPageList<DtoLink> GetLinkList(DtoPageRequest request)
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
