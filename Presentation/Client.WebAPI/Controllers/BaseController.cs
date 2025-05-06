using Application.Core.Interfaces.Basic;
using Application.Model.Basic.Base;
using Common;
using IdentifierGenerator;
using Microsoft.AspNetCore.Mvc;

namespace Client.WebAPI.Controllers
{
    /// <summary>
    /// 系统基础方法控制器
    /// </summary>
    [Route("[controller]/[action]")]
    [ApiController]
    public class BaseController(IBaseService baseService, IdService idService) : ControllerBase
    {


        /// <summary>
        /// 获取省市级联地址数据
        /// </summary>
        /// <param name="provinceId">省份ID</param>
        /// <param name="cityId">城市ID</param>
        /// <returns></returns>
        /// <remarks>不传递任何参数返回省份数据，传入省份ID返回城市数据，传入城市ID返回区域数据</remarks>
        [HttpGet]
        public Task<List<DtoRegion>> GetRegion(int provinceId, int cityId) => baseService.GetRegionAsync(provinceId, cityId);



        /// <summary>
        /// 获取全部省市级联地址数据
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public Task<List<DtoRegion>> GetRegionAll() => baseService.GetRegionAllAsync();



        /// <summary>
        /// 二维码生成
        /// </summary>
        /// <param name="text">数据内容</param>
        /// <returns></returns>
        [HttpGet]
        public FileResult GetQrCode(string text)
        {
            var image = ImgHelper.GetQrCode(text);
            return File(image, "image/png");
        }



        /// <summary>
        /// 图像验证码生成
        /// </summary>
        /// <param name="sign">标记</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<FileResult> GetVerifyCode(Guid sign)
        {
            var image = await baseService.GetVerifyCodeAsync(sign);
            return File(image, "image/png");
        }



        /// <summary>
        /// 获取指定组ID的KV键值对
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        [HttpGet]
        public Task<Dictionary<string, string>> GetValueList(long groupId) => baseService.GetValueListAsync(groupId);



        /// <summary>
        /// 获取一个雪花ID
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public long GetSnowflakeId() => idService.GetId();


    }
}