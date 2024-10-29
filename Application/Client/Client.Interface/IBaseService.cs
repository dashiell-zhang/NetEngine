using Shared.Model;

namespace Client.Interface
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
        public List<DtoKeyValue> GetRegion(int provinceId, int cityId);


        /// <summary>
        /// 获取全部省市级联地址数据
        /// </summary>
        /// <returns></returns>
        public List<DtoKeyValueChild> GetRegionAll();


        /// <summary>
        /// 图像验证码生成
        /// </summary>
        /// <param name="sign">标记</param>
        /// <returns></returns>
        public byte[] GetVerifyCode(Guid sign);


        /// <summary>
        /// 获取指定组ID的KV键值对
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        public List<DtoKeyValue> GetValueList(long groupId);




    }
}
