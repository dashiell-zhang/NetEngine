using Application.Model.Basic.Base;

namespace Application.Interface.Basic;

/// <summary>
/// 公共基础数据查询与验证码生成
/// </summary>
public interface IBaseService
{

    /// <summary>
    /// 获取省市级联地址数据
    /// </summary>
    Task<List<RegionDto>> GetRegionAsync(int provinceId, int cityId);


    /// <summary>
    /// 获取全部省市级联地址数据
    /// </summary>
    Task<List<RegionDto>> GetRegionAllAsync();


    /// <summary>
    /// 获取指定组ID的KV键值对
    /// </summary>
    Task<Dictionary<string, string>> GetValueListAsync(long groupId);


    /// <summary>
    /// 生成图像验证码
    /// </summary>
    Task<byte[]> GetVerifyCodeAsync(Guid sign);

}
