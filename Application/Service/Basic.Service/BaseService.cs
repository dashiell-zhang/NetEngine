using Basic.Interface;
using Basic.Model.Base;
using Common;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;

namespace Basic.Service
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class BaseService(DatabaseContext db, IDistributedCache distributedCache) : IBaseService
    {
        public List<DtoRegion> GetRegion(int provinceId, int cityId)
        {
            List<DtoRegion> list = [];

            if (provinceId == 0 && cityId == 0)
            {
                list = db.TRegionProvince.Select(t => new DtoRegion { Id = t.Id, Name = t.Province }).ToList();
            }

            if (provinceId != 0)
            {
                list = db.TRegionCity.Where(t => t.ProvinceId == provinceId).Select(t => new DtoRegion { Id = t.Id, Name = t.City }).ToList();
            }

            if (cityId != 0)
            {
                list = db.TRegionArea.Where(t => t.CityId == cityId).Select(t => new DtoRegion { Id = t.Id, Name = t.Area }).ToList();
            }

            return list;
        }


        public List<DtoRegion> GetRegionAll()
        {
            var list = db.TRegionProvince.Select(t => new DtoRegion
            {
                Id = t.Id,
                Name = t.Province,
                ChildList = t.TRegionCity!.Select(c => new DtoRegion
                {
                    Id = c.Id,
                    Name = c.City,
                    ChildList = c.TRegionArea!.Select(a => new DtoRegion
                    {
                        Id = a.Id,
                        Name = a.Area
                    }).ToList()
                }).ToList()
            }).ToList();

            return list;
        }


        public Dictionary<string, string> GetValueList(long groupId)
        {
            Dictionary<string, string> keyValuePairs = new();

            var list = db.TAppSetting.Where(t => t.Module == "Dictionary" && t.GroupId == groupId).Select(t => new
            {
                t.Key,
                t.Value
            }).ToList();

            foreach (var item in list)
            {
                keyValuePairs.Add(item.Key, item.Value);
            }

            return keyValuePairs;
        }


        public byte[] GetVerifyCode(Guid sign)
        {
            var cacheKey = "VerifyCode" + sign.ToString();
            Random random = new();
            string text = random.Next(1000, 9999).ToString();

            var image = ImgHelper.GetVerifyCode(text);

            distributedCache.Set(cacheKey, text, TimeSpan.FromMinutes(5));

            return image;
        }
    }
}
