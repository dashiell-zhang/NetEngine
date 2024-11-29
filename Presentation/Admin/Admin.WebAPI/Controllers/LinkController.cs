using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Model;
using Site.Interface;
using Site.Model.Link;
using WebAPI.Core.Filters;

namespace Admin.WebAPI.Controllers
{
    [SignVerifyFilter]
    [Route("[controller]/[action]")]
    [Authorize]
    [ApiController]
    public class LinkController(ILinkService linkService) : ControllerBase
    {


        /// <summary>
        /// 获取友情链接列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpGet]
        public DtoPageList<DtoLink> GetLinkList([FromQuery] DtoPageRequest request) => linkService.GetLinkList(request);



        /// <summary>
        /// 获取友情链接
        /// </summary>
        /// <param name="linkId">链接ID</param>
        /// <returns></returns>
        [HttpGet]
        public DtoLink? GetLink(long linkId) => linkService.GetLink(linkId);



        /// <summary>
        /// 创建友情链接
        /// </summary>
        /// <param name="createLink"></param>
        /// <returns></returns>
        [HttpPost]
        public long CreateLink(DtoEditLink createLink) => linkService.CreateLink(createLink);



        /// <summary>
        /// 更新友情链接
        /// </summary>
        /// <param name="linkId"></param>
        /// <param name="updateLink"></param>
        /// <returns></returns>
        [HttpPost]
        public bool UpdateLink(long linkId, DtoEditLink updateLink) => linkService.UpdateLink(linkId, updateLink);



        /// <summary>
        /// 删除友情链接
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        public bool DeleteLink(long id) => linkService.DeleteLink(id);


    }
}
