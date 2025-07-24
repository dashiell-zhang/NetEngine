using Application.Model.Basic.Base;

namespace Application.Interface.Basic
{
    public interface IBaseService
    {

        /// <summary>
        /// 获取省市级联地址数据
        /// </summary>
        /// <param name="provinceId">省份ID</param>
        /// <param name="cityId">城市ID</param>
        /// <returns></returns>
        /// <remarks>不传递任何参数返回省份数据，传入省份ID返回城市数据，传入城市ID返回区域数据</remarks>
        Task<List<DtoRegion>> GetRegionAsync(int provinceId, int cityId);


        /// <summary>
        /// 获取全部省市级联地址数据
        /// </summary>
        /// <returns></returns>
        Task<List<DtoRegion>> GetRegionAllAsync();


        /// <summary>
        /// 图像验证码生成
        /// </summary>
        /// <param name="sign">标记</param>
        /// <returns></returns>
        Task<byte[]> GetVerifyCodeAsync(Guid sign);


        /// <summary>
        /// 获取指定组ID的KV键值对
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        Task<Dictionary<string, string>> GetValueListAsync(long groupId);

    }
}
