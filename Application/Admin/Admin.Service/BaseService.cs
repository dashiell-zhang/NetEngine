using Admin.Interface;
using Common;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using Shared.Model;

namespace Admin.Service
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class BaseService(DatabaseContext db) : IBaseService
    {


        public List<DtoKeyValue> GetRegion(int provinceId, int cityId)
        {
            List<DtoKeyValue> list = [];

            if (provinceId == 0 && cityId == 0)
            {
                list = db.TRegionProvince.Select(t => new DtoKeyValue { Key = t.Id, Value = t.Province }).ToList();
            }

            if (provinceId != 0)
            {
                list = db.TRegionCity.Where(t => t.ProvinceId == provinceId).Select(t => new DtoKeyValue { Key = t.Id, Value = t.City }).ToList();
            }

            if (cityId != 0)
            {
                list = db.TRegionArea.Where(t => t.CityId == cityId).Select(t => new DtoKeyValue { Key = t.Id, Value = t.Area }).ToList();
            }

            return list;
        }



        public List<DtoKeyValueChild> GetRegionAll()
        {

            var list = db.TRegionProvince.Select(t => new DtoKeyValueChild
            {
                Key = t.Id,
                Value = t.Province,
                ChildList = t.TRegionCity!.Select(c => new DtoKeyValueChild
                {
                    Key = c.Id,
                    Value = c.City,
                    ChildList = c.TRegionArea!.Select(a => new DtoKeyValueChild
                    {
                        Key = a.Id,
                        Value = a.Area
                    }).ToList()
                }).ToList()
            }).ToList();

            return list;
        }


        public List<DtoKeyValue> GetValueList(long groupId)
        {

            var list = db.TAppSetting.Where(t => t.Module == "Dictionary" && t.GroupId == groupId).Select(t => new DtoKeyValue
            {
                Key = t.Key,
                Value = t.Value
            }).ToList();

            return list;
        }
    }
}
