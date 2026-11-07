using WebAPI.Core.Extensions;
using Application.Model.Authorize;
using Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using WebAPI.Core.Filters;

namespace Admin.WebAPI.Controllers;


/// <summary>
/// 系统访问授权模块
/// </summary>
[Route("[controller]/[action]")]
[ApiController]
public class AuthorizeController(IAuthorizeService authorizeService, IAuthorizeMaintenanceService maintenanceService) : ControllerBase
{


    /// <summary>
    /// 获取Token认证信息
    /// </summary>
    /// <param name="login">登录信息集合</param>
    /// <returns></returns>
    [HttpPost]
    public Task<string?> GetToken(GetTokenDto login) => authorizeService.GetTokenAsync(login);



    /// <summary>
    /// 获取授权功能列表
    /// </summary>
    /// <returns></returns>
    [SignVerifyFilter]
    [Authorize]
    [HttpGet]
    public Task<Dictionary<string, string>> GetFunctionList(string? sign) => authorizeService.GetFunctionListAsync(User.GetUserId(), sign);



    /// <summary>
    /// 更新路由信息表
    /// </summary>
    /// <param name="actionDescriptorCollectionProvider"></param>
    [HttpGet]
    public async Task UpdateRoute(IActionDescriptorCollectionProvider actionDescriptorCollectionProvider)
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


        var routes = actionList.Select(item => new AuthorizeRouteDto
        {
            Route = item.Route!,
            Remark = remarksDict.GetValueOrDefault(item.Name)
        }).ToList();

        await maintenanceService.UpdateRoutesAsync(projectName, routes);

    }


    /// <summary>
    /// 生成 ECDSA 公私钥
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public object GenerateEcdsaKeyPair()
    {
        var env = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();

        if (!env.IsDevelopment())
        {
            throw new InvalidOperationException("GenerateEcdsaKeyPair 仅允许在 Development 环境执行");
        }

        using var keyInfo = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        return new
        {
            PrivateKey = Convert.ToBase64String(keyInfo.ExportECPrivateKey()),
            PublicKey = Convert.ToBase64String(keyInfo.ExportSubjectPublicKeyInfo())
        };
    }


    /// <summary>
    /// 初始化权限基础数据
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    public async Task<object> InitData()
    {

        var env = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();

        if (!env.IsDevelopment())
        {
            throw new InvalidOperationException("InitData 仅允许在 Development 环境执行");
        }

        string filePath = Path.Combine(AppContext.BaseDirectory, "InitData", "AuthorizeInitData.json");

        if (!System.IO.File.Exists(filePath))
        {
            throw new FileNotFoundException("未找到初始化数据文件", filePath);
        }

        var json = await System.IO.File.ReadAllTextAsync(filePath);
        var seedData = JsonSerializer.Deserialize<AuthorizeSeedData>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (seedData == null)
        {
            throw new InvalidOperationException("初始化数据文件解析失败");
        }

        var result = await maintenanceService.InitializeAsync(seedData);

        return new
        {
            result.AdminUser,
            result.AdminRole,
            result.FunctionCount,
            result.FunctionRouteCount,
            result.FunctionAuthorizeCount,
            result.UserRoleCount,
            FilePath = filePath
        };

    }

}
