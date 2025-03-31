using Basic.Interface;
using Basic.Model.Base;
using Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;

namespace Basic.Service
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class BaseService(DatabaseContext db, IDistributedCache distributedCache) : IBaseService
    {
        public async Task<List<DtoRegion>> GetRegionAsync(int provinceId, int cityId)
        {
            List<DtoRegion> list = [];

            if (provinceId == 0 && cityId == 0)
            {
                list = await db.TRegionProvince.Select(t => new DtoRegion { Id = t.Id, Name = t.Province }).ToListAsync();
            }

            if (provinceId != 0)
            {
                list = await db.TRegionCity.Where(t => t.ProvinceId == provinceId).Select(t => new DtoRegion { Id = t.Id, Name = t.City }).ToListAsync();
            }

            if (cityId != 0)
            {
                list = await db.TRegionArea.Where(t => t.CityId == cityId).Select(t => new DtoRegion { Id = t.Id, Name = t.Area }).ToListAsync();
            }

            return list;
        }


        public async Task<List<DtoRegion>> GetRegionAllAsync()
        {
            var list = await db.TRegionProvince.Select(t => new DtoRegion
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
            }).ToListAsync();

            return list;
        }


        public async Task<Dictionary<string, string>> GetValueListAsync(long groupId)
        {
            Dictionary<string, string> keyValuePairs = [];

            var list = await db.TAppSetting.Where(t => t.Module == "Dictionary" && t.GroupId == groupId).Select(t => new
            {
                t.Key,
                t.Value
            }).ToListAsync();

            foreach (var item in list)
            {
                keyValuePairs.Add(item.Key, item.Value);
            }

            return keyValuePairs;
        }


        public async Task<byte[]> GetVerifyCodeAsync(Guid sign)
        {
            var cacheKey = "VerifyCode" + sign.ToString();
            Random random = new();
            string text = random.Next(1000, 9999).ToString();

            var image = ImgHelper.GetVerifyCode(text);

            await distributedCache.SetAsync(cacheKey, text, TimeSpan.FromMinutes(5));

            return image;
        }


    }
}
