using Application.Model.Basic.Base;
using Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using SourceGenerator.Runtime.Attributes;

namespace Application.Service.Basic;
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class BaseService(DatabaseContext db, IDistributedCache distributedCache)
{

    /// <summary>
    /// 获取省市级联地址数据
    /// </summary>
    /// <param name="provinceId">省份ID</param>
    /// <param name="cityId">城市ID</param>
    /// <returns></returns>
    /// <remarks>不传递任何参数返回省份数据，传入省份ID返回城市数据，传入城市ID返回区域数据</remarks>
    public async Task<List<RegionDto>> GetRegionAsync(int provinceId, int cityId)
    {
        List<RegionDto> list = [];

        if (provinceId == 0 && cityId == 0)
        {
            list = await db.RegionProvince.Select(t => new RegionDto { Id = t.Id, Name = t.Province }).ToListAsync();
        }

        if (provinceId != 0)
        {
            list = await db.RegionCity.Where(t => t.ProvinceId == provinceId).Select(t => new RegionDto { Id = t.Id, Name = t.City }).ToListAsync();
        }

        if (cityId != 0)
        {
            list = await db.RegionArea.Where(t => t.CityId == cityId).Select(t => new RegionDto { Id = t.Id, Name = t.Area }).ToListAsync();
        }

        return list;
    }


    /// <summary>
    /// 获取全部省市级联地址数据
    /// </summary>
    /// <returns></returns>
    public async Task<List<RegionDto>> GetRegionAllAsync()
    {
        var list = await db.RegionProvince.Select(t => new RegionDto
        {
            Id = t.Id,
            Name = t.Province,
            ChildList = t.TRegionCity!.Select(c => new RegionDto
            {
                Id = c.Id,
                Name = c.City,
                ChildList = c.TRegionArea!.Select(a => new RegionDto
                {
                    Id = a.Id,
                    Name = a.Area
                }).ToList()
            }).ToList()
        }).ToListAsync();

        return list;
    }


    /// <summary>
    /// 图像验证码生成
    /// </summary>
    /// <param name="sign">标记</param>
    /// <returns></returns>
    public async Task<Dictionary<string, string>> GetValueListAsync(long groupId)
    {
        Dictionary<string, string> keyValuePairs = [];

        var list = await db.AppSetting.Where(t => t.Module == "Dictionary" && t.GroupId == groupId).Select(t => new
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


    /// <summary>
    /// 获取指定组ID的KV键值对
    /// </summary>
    /// <param name="groupId"></param>
    /// <returns></returns>
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
