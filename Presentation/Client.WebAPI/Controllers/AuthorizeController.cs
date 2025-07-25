using Application.Model.Authorize.Authorize;
using Application.Service.Authorize;
using IdentifierGenerator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Repository.Database;
using System.Xml;
using WebAPI.Core.Filters;

namespace Client.WebAPI.Controllers
{


    /// <summary>
    /// 系统访问授权模块
    /// </summary>
    [Route("[controller]/[action]")]
    [ApiController]
    public class AuthorizeController(AuthorizeService authorizeService) : ControllerBase
    {


        /// <summary>
        /// 获取公钥
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpGet]
        public string? GetPublicKey() => authorizeService.GetPublicKey();



        /// <summary>
        /// 获取Token认证信息
        /// </summary>
        /// <param name="login"></param>
        /// <returns></returns>
        [HttpPost]
        public Task<string?> GetToken(DtoGetToken login) => authorizeService.GetTokenAsync(login);



        /// <summary>
        /// 通过微信小程序Code获取Token认证信息
        /// </summary>
        /// <param name="login"></param>
        /// <returns></returns>
        [HttpPost]
        public Task<string?> GetTokenByWeiXinMiniApp([FromBody] DtoGetTokenByWeiXinApp login) => authorizeService.GetTokenByWeiXinMiniAppAsync(login);



        /// <summary>
        /// 利用手机号和短信验证码获取Token认证信息
        /// </summary>
        /// <param name="login"></param>
        /// <returns></returns>
        [HttpPost]
        public Task<string?> GetTokenBySMS(DtoGetTokenBySMS login) => authorizeService.GetTokenBySMSAsync(login);



        /// <summary>
        /// 获取授权功能列表
        /// </summary>
        /// <param name="sign">模块标记</param>
        /// <returns></returns>
        [Authorize]
        [CacheDataFilter(TTL = 60, IsUseToken = true)]
        [HttpGet]
        public Task<Dictionary<string, string>> GetFunctionList(string sign) => authorizeService.GetFunctionListAsync(sign);



        /// <summary>
        /// 发送短信验证手机号码所有权
        /// </summary>
        /// <param name="sendVerifyCode"></param>
        /// <returns></returns>
        [HttpPost]
        public Task<bool> SendSMSVerifyCode(DtoSendSMSVerifyCode sendVerifyCode) => authorizeService.SendSMSVerifyCodeAsync(sendVerifyCode);



        /// <summary>
        /// 通过微信App Code获取Token认证信息
        /// </summary>
        /// <param name="login"></param>
        /// <returns></returns>
        [HttpPost]
        public Task<string?> GetTokenByWeiXinApp(DtoGetTokenByWeiXinApp login) => authorizeService.GetTokenByWeiXinAppAsync(login);



        /// <summary>
        /// 通过老密码修改密码
        /// </summary>
        /// <param name="updatePassword"></param>
        /// <returns></returns>
        [Authorize]
        [QueueLimitFilter(IsBlock = true, IsUseParameter = false, IsUseToken = true)]
        [HttpPost]
        public Task<bool> UpdatePasswordByOldPassword(DtoUpdatePasswordByOldPassword updatePassword) => authorizeService.UpdatePasswordByOldPasswordAsync(updatePassword);



        /// <summary>
        /// 通过短信验证码修改账户密码</summary>
        /// <param name="updatePassword"></param>
        /// <returns></returns>
        [Authorize]
        [QueueLimitFilter(IsBlock = true, IsUseParameter = false, IsUseToken = true)]
        [HttpPost]
        public Task<bool> UpdatePasswordBySMS(DtoUpdatePasswordBySMS updatePassword) => authorizeService.UpdatePasswordBySMSAsync(updatePassword);



        /// <summary>
        /// 更新路由信息表
        /// </summary>
        [HttpGet]
        public async void UpdateRoute(IActionDescriptorCollectionProvider actionDescriptorCollectionProvider, DatabaseContext db, IdService idService)
        {
            var actionList = actionDescriptorCollectionProvider.ActionDescriptors.Items.Cast<ControllerActionDescriptor>().Select(x => new
            {
                Name = x.DisplayName![..(x.DisplayName!.IndexOf('(') - 1)],
                Route = x.AttributeRouteInfo!.Template,
                IsAuthorize = x.EndpointMetadata.Where(t => t.GetType().FullName == "Microsoft.AspNetCore.Authorization.AuthorizeAttribute").Any() == true && x.EndpointMetadata.Where(t => t.GetType().FullName == "Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute").Any() == false,
            }).ToList();

            string projectName = typeof(Program).Assembly.GetName().Name!;

            XmlDocument xml = new();
            xml.Load(AppContext.BaseDirectory + projectName + ".xml");
            XmlNodeList memebers = xml.SelectNodes("/doc/members/member")!;

            Dictionary<string, string> remarksDict = [];


            for (int c = 0; c < memebers.Count; c++)
            {
                var xmlNode = memebers[c];

                if (xmlNode != null)
                {
                    if (xmlNode.Attributes!["name"]!.Value.StartsWith("M:" + projectName + ".Controllers."))
                    {
                        for (int s = 0; s < xmlNode.ChildNodes.Count; s++)
                        {
                            var childNode = xmlNode.ChildNodes[s];

                            if (childNode != null && childNode.Name == "summary")
                            {
                                string name = xmlNode.Attributes!["name"]!.Value;

                                string summary = childNode.InnerText;

                                name = name![2..];

                                if (name.Contains('(', StringComparison.CurrentCulture))
                                {
                                    name = name[..name.IndexOf('(')];
                                }

                                summary = summary.Replace("\n", "").Trim();

                                remarksDict.Add(name, summary);
                            }
                        }
                    }
                }
            }


            actionList = [.. actionList.Where(t => t.IsAuthorize == true).Distinct()];


            var functionRoutes = await db.TFunctionRoute.Where(t => t.Module == projectName).ToListAsync();

            var delList = functionRoutes.Where(t => actionList.Select(t => t.Route).ToList().Contains(t.Route) == false).ToList();

            foreach (var item in delList)
            {
                item.IsDelete = true;
            }

            foreach (var item in actionList)
            {
                var info = functionRoutes.Where(t => t.Route == item.Route).FirstOrDefault();

                string? remarks = remarksDict.Where(a => a.Key == item.Name).Select(a => a.Value).FirstOrDefault();

                if (info != null)
                {
                    info.Remarks = remarks;
                }
                else
                {
                    TFunctionRoute functionRoute = new()
                    {
                        Id = idService.GetId(),
                        Module = projectName,
                        Route = item.Route!,
                        Remarks = remarks
                    };

                    db.TFunctionRoute.Add(functionRoute);
                }
            }

            await db.SaveChangesAsync();

        }

    }
}
