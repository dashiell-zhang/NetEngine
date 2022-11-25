using AdminAPI.Filters;
using AdminAPI.Libraries;
using AdminAPI.Services;
using AdminShared.Models.Authorize;
using Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Repository.Database;
using System.Text;
using System.Xml;

namespace AdminAPI.Controllers
{


    /// <summary>
    /// 系统访问授权模块
    /// </summary>
    [Route("[controller]")]
    [ApiController]
    public class AuthorizeController : ControllerBase
    {


        private readonly DatabaseContext db;

        private readonly AuthorizeService authorizeService;

        private readonly long userId;

        private readonly IDHelper idHelper;




        public AuthorizeController(DatabaseContext db, AuthorizeService authorizeService, IHttpContextAccessor httpContextAccessor, IDHelper idHelper)
        {
            this.db = db;

            this.authorizeService = authorizeService;

            var userIdStr = httpContextAccessor.HttpContext?.GetClaimByAuthorization("userId");
            if (userIdStr != null)
            {
                userId = long.Parse(userIdStr);
            }

            this.idHelper = idHelper;
        }





        /// <summary>
        /// 获取Token认证信息
        /// </summary>
        /// <param name="login">登录信息集合</param>
        /// <returns></returns>
        [HttpPost("GetToken")]
        public string? GetToken(DtoLogin login)
        {
            var userList = db.TUser.Where(t => t.IsDelete == false && (t.Name == login.Name || t.Phone == login.Name || t.Email == login.Name)).Select(t => new { t.Id, t.PassWord }).ToList();

            var user = userList.Where(t => t.PassWord == Convert.ToBase64String(KeyDerivation.Pbkdf2(login.PassWord, Encoding.UTF8.GetBytes(t.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32))).FirstOrDefault();

            if (user != null)
            {
                return authorizeService.GetTokenByUserId(user.Id);
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
                HttpContext.Items.Add("errMsg", "用户名或密码错误");

                return default;
            }

        }




        /// <summary>
        /// 获取授权功能列表
        /// </summary>
        /// <returns></returns>
        [SignVerifyFilter]
        [Authorize]
        [HttpGet("GetFunctionList")]
        public List<string> GetFunctionList()
        {
            var roleIds = db.TUserRole.AsNoTracking().Where(t => t.IsDelete == false && t.UserId == userId).Select(t => t.RoleId).ToList();

            var kvList = db.TFunctionAuthorize.Where(t => t.IsDelete == false && (roleIds.Contains(t.RoleId!.Value) || t.UserId == userId)).Select(t =>
                t.Function.Sign
            ).ToList();

            return kvList;
        }




        /// <summary>
        /// 更新路由信息表
        /// </summary>
        /// <param name="actionDescriptorCollectionProvider"></param>
        [HttpGet("UpdateRoute")]
        public void UpdateRoute([FromServices] IActionDescriptorCollectionProvider actionDescriptorCollectionProvider)
        {
            var actionList = actionDescriptorCollectionProvider.ActionDescriptors.Items.Cast<ControllerActionDescriptor>().Select(x => new
            {
                Name = x.DisplayName![..(x.DisplayName!.IndexOf("(") - 1)],
                Route = x.AttributeRouteInfo!.Template,
                IsAuthorize = (x.EndpointMetadata.Where(t => t.GetType().FullName == "Microsoft.AspNetCore.Authorization.AuthorizeAttribute").Any() == true && x.EndpointMetadata.Where(t => t.GetType().FullName == "Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute").Any() == false),
            }).ToList();

            string projectName = typeof(Program).Assembly.GetName().Name!;

            XmlDocument xml = new();
            xml.Load(AppContext.BaseDirectory + projectName + ".xml");
            XmlNodeList memebers = xml.SelectNodes("/doc/members/member")!;

            Dictionary<string, string> remarksDict = new();


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
                                    name = name[..name.IndexOf("(")];
                                }

                                summary = summary.Replace("\n", "").Trim();

                                remarksDict.Add(name, summary);
                            }
                        }
                    }
                }
            }


            actionList = actionList.Where(t => t.IsAuthorize == true).Distinct().ToList();


            var functionRoutes = db.TFunctionRoute.Where(t => t.IsDelete == false && t.Module == projectName).ToList();

            var delList = functionRoutes.Where(t => actionList.Select(t => t.Route).ToList().Contains(t.Route) == false).ToList();

            foreach (var item in delList)
            {
                item.IsDelete = true;
                item.DeleteTime = DateTime.UtcNow;
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
                        Id = idHelper.GetId(),
                        CreateTime = DateTime.UtcNow,
                        Module = projectName,
                        Route = item.Route!,
                        Remarks = remarks
                    };

                    db.TFunctionRoute.Add(functionRoute);
                }
            }

            db.SaveChanges();

        }


    }
}
