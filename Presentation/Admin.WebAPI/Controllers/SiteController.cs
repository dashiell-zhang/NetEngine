using Application.Model.Shared;
using Application.Model.Site.Link;
using Application.Model.Site.Site;
using Application.Service.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Core.Filters;

namespace Admin.WebAPI.Controllers;

/// <summary>
/// 站点控制器
/// </summary>
[SignVerifyFilter]
[Authorize]
[Route("[controller]/[action]")]
[ApiController]
public class SiteController(SiteService siteService, LinkService linkService) : ControllerBase
{


    /// <summary>
    /// 获取站点信息
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public Task<SiteDto> GetSite() => siteService.GetSiteAsync();



    /// <summary>
    /// 编辑站点信息
    /// </summary>
    /// <param name="editSite"></param>
    /// <returns></returns>
    [HttpPost]
    public Task<bool> EditSite(SiteDto editSite) => siteService.EditSiteAsync(editSite);



    /// <summary>
    /// 获取友情链接列表
    /// </summary>
    [HttpGet]
    public Task<PageListDto<LinkDto>> GetLinkList([FromQuery] PageRequestDto request) => linkService.GetLinkListAsync(request);


    /// <summary>
    /// 获取友情链接
    /// </summary>
    /// <param name="linkId">链接ID</param>
    [HttpGet]
    public Task<LinkDto?> GetLink(long linkId) => linkService.GetLinkAsync(linkId);


    /// <summary>
    /// 创建友情链接
    /// </summary>
    [HttpPost]
    public Task<long> CreateLink(EditLinkDto createLink) => linkService.CreateLinkAsync(createLink);


    /// <summary>
    /// 更新友情链接
    /// </summary>
    [HttpPost]
    public Task<bool> UpdateLink(long linkId, EditLinkDto updateLink) => linkService.UpdateLinkAsync(linkId, updateLink);


    /// <summary>
    /// 删除友情链接
    /// </summary>
    [HttpDelete]
    public Task<bool> DeleteLink(long id) => linkService.DeleteLinkAsync(id);



    /// <summary>
    /// 获取服务器信息
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public List<ServerInfoDto> GetServerInfo()
    {
        List<ServerInfoDto> list =
        [
            new ServerInfoDto
            {
                Name = "服务器名称",
                Value = Environment.MachineName
            },

            new ServerInfoDto
            {
                Name = "服务器IP",
                Value = HttpContext.Connection.LocalIpAddress!.ToString()
            },

            new ServerInfoDto
            {
                Name = "操作系统",
                Value = Environment.OSVersion.ToString()
            },

            new ServerInfoDto
            {
                Name = "外部端口",
                Value = HttpContext.Connection.LocalPort.ToString()
            },

            new ServerInfoDto
            {
                Name = "目录物理路径",
                Value = Environment.CurrentDirectory
            },

            new ServerInfoDto
            {
                Name = "服务器CPU",
                Value = Environment.ProcessorCount.ToString() + "核"
            },

            new ServerInfoDto
            {
                Name = "本网站占用内存",
                Value = ((double)GC.GetTotalMemory(false) / 1048576).ToString("N2") + "M"
            }
        ];

        return list;
    }


}
