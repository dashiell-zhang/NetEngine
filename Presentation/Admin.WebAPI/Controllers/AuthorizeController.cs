using Application.Model.Authorize;
using Application.Service;
using IdentifierGenerator;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Repository;
using Repository.Database;
using Repository.Database.Enums;
using System.Text;
using System.Text.Json;
using System.Xml;
using WebAPI.Core.Filters;

namespace Admin.WebAPI.Controllers;


/// <summary>
/// 系统访问授权模块
/// </summary>
[Route("[controller]/[action]")]
[ApiController]
public class AuthorizeController(AuthorizeService authorizeService, DatabaseContext db, IdService idService) : ControllerBase
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
    public Task<Dictionary<string, string>> GetFunctionList(string? sign) => authorizeService.GetFunctionListAsync(sign);



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


        var functionRoutes = await db.FunctionRoute.Where(t => t.Module == projectName).ToListAsync();

        var delList = functionRoutes.Where(t => actionList.Select(t => t.Route).ToList().Contains(t.Route) == false).ToList();

        foreach (var item in delList)
        {
            item.DeleteTime = DateTimeOffset.UtcNow;
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
                FunctionRoute functionRoute = new()
                {
                    Id = idService.GetId(),
                    Module = projectName,
                    Route = item.Route!,
                    Remarks = remarks
                };

                db.FunctionRoute.Add(functionRoute);
            }
        }

        await db.SaveChangesAsync();

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

        await using var transaction = await db.Database.BeginTransactionAsync();

        var adminUser = await UpsertAdminUserAsync(seedData.AdminUser);
        var adminRole = await UpsertAdminRoleAsync(seedData.AdminRole);
        var functionDict = await UpsertFunctionsAsync(seedData.Functions);
        await UpsertFunctionRoutesAsync(seedData.FunctionRoutes, functionDict);
        await EnsureAdminUserRoleAsync(adminUser.Id, adminRole.Id);
        await EnsureAdminRoleFunctionAuthorizeAsync(adminUser.Id, adminRole.Id);

        await transaction.CommitAsync();

        return new
        {
            AdminUser = adminUser.UserName,
            AdminRole = adminRole.Code,
            FunctionCount = seedData.Functions.Count,
            FunctionRouteCount = seedData.FunctionRoutes.Count,
            FunctionAuthorizeCount = await db.FunctionAuthorize.Where(t => t.RoleId == adminRole.Id && t.UserId == null).CountAsync(),
            UserRoleCount = await db.UserRole.Where(t => t.UserId == adminUser.Id).CountAsync(),
            FilePath = filePath
        };
    }


    /// <summary>
    /// 初始化管理员账号
    /// </summary>
    /// <param name="adminUser"></param>
    /// <returns></returns>
    private async Task<Repository.Database.User> UpsertAdminUserAsync(AdminUserSeedItem adminUser)
    {
        var user = await db.User.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.UserName == adminUser.UserName);

        if (user == null)
        {
            user = new Repository.Database.User
            {
                Id = idService.GetId()
            };

            db.User.Add(user);
        }

        user.Name = adminUser.Name;
        user.UserName = adminUser.UserName;
        user.Phone = adminUser.Phone;
        user.Email = adminUser.Email;
        user.Password = Convert.ToBase64String(KeyDerivation.Pbkdf2(adminUser.Password, Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));
        user.DeleteTime = null;
        user.DeleteUserId = null;

        await db.SaveChangesAsync();

        return user;
    }


    /// <summary>
    /// 初始化管理员角色
    /// </summary>
    /// <param name="adminRole"></param>
    /// <returns></returns>
    private async Task<Role> UpsertAdminRoleAsync(AdminRoleSeedItem adminRole)
    {
        var role = await db.Role.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Code == adminRole.Code);

        if (role == null)
        {
            role = new Role
            {
                Id = idService.GetId()
            };

            db.Role.Add(role);
        }

        role.Code = adminRole.Code;
        role.Name = adminRole.Name;
        role.Remarks = adminRole.Remarks;
        role.DeleteTime = null;

        await db.SaveChangesAsync();

        return role;
    }


    /// <summary>
    /// 初始化功能数据
    /// </summary>
    /// <param name="functions"></param>
    /// <returns></returns>
    private async Task<Dictionary<string, Function>> UpsertFunctionsAsync(List<FunctionSeedItem> functions)
    {
        var signs = functions.Select(t => t.Sign).ToList();
        var dbFunctions = await db.Function.IgnoreQueryFilters().Where(t => signs.Contains(t.Sign)).ToDictionaryAsync(t => t.Sign);

        foreach (var item in functions)
        {
            if (!dbFunctions.TryGetValue(item.Sign, out var function))
            {
                function = new Function
                {
                    Id = idService.GetId()
                };

                db.Function.Add(function);
                dbFunctions.Add(item.Sign, function);
            }

            function.Name = item.Name;
            function.Sign = item.Sign;
            function.Remarks = item.Remarks;
            function.Type = (FunctionType)item.Type;
            function.DeleteTime = null;
        }

        foreach (var item in functions)
        {
            dbFunctions[item.Sign].ParentId = item.ParentSign == null ? null : dbFunctions[item.ParentSign].Id;
        }

        await db.SaveChangesAsync();

        return dbFunctions;
    }


    /// <summary>
    /// 初始化路由功能映射数据
    /// </summary>
    /// <param name="functionRoutes"></param>
    /// <param name="functionDict"></param>
    /// <returns></returns>
    private async Task UpsertFunctionRoutesAsync(List<FunctionRouteSeedItem> functionRoutes, Dictionary<string, Function> functionDict)
    {
        var modules = functionRoutes.Select(t => t.Module).Distinct().ToList();
        var routes = functionRoutes.Select(t => t.Route).Distinct().ToList();
        var dbFunctionRoutes = await db.FunctionRoute.IgnoreQueryFilters()
            .Where(t => modules.Contains(t.Module) && routes.Contains(t.Route))
            .ToListAsync();

        var routeDict = dbFunctionRoutes.ToDictionary(t => t.Module + "|" + t.Route, t => t);

        foreach (var item in functionRoutes)
        {
            string routeKey = item.Module + "|" + item.Route;

            if (!routeDict.TryGetValue(routeKey, out var functionRoute))
            {
                functionRoute = new FunctionRoute
                {
                    Id = idService.GetId()
                };

                db.FunctionRoute.Add(functionRoute);
                routeDict.Add(routeKey, functionRoute);
            }

            functionRoute.FunctionId = item.FunctionSign == null ? null : functionDict[item.FunctionSign].Id;
            functionRoute.Module = item.Module;
            functionRoute.Route = item.Route;
            functionRoute.Remarks = item.Remarks;
            functionRoute.DeleteTime = null;
        }

        await db.SaveChangesAsync();
    }


    /// <summary>
    /// 绑定管理员角色
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="roleId"></param>
    /// <returns></returns>
    private async Task EnsureAdminUserRoleAsync(long userId, long roleId)
    {
        var userRole = await db.UserRole.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.UserId == userId && t.RoleId == roleId);

        if (userRole == null)
        {
            userRole = new UserRole
            {
                Id = idService.GetId(),
                UserId = userId,
                RoleId = roleId,
                CreateUserId = userId
            };

            db.UserRole.Add(userRole);
        }
        else
        {
            userRole.DeleteTime = null;
            userRole.DeleteUserId = null;
        }

        var otherUserRoleList = await db.UserRole.Where(t => t.UserId == userId && t.RoleId != roleId).ToListAsync();

        foreach (var item in otherUserRoleList)
        {
            item.DeleteTime = DateTimeOffset.UtcNow;
            item.DeleteUserId = userId;
        }

        await db.SaveChangesAsync();
    }


    /// <summary>
    /// 授予管理员全部功能权限
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="roleId"></param>
    /// <returns></returns>
    private async Task EnsureAdminRoleFunctionAuthorizeAsync(long userId, long roleId)
    {
        var functionIdList = await db.Function.Select(t => t.Id).ToListAsync();
        var dbFunctionAuthorizes = await db.FunctionAuthorize.IgnoreQueryFilters().Where(t => t.RoleId == roleId && t.UserId == null).ToListAsync();
        var functionAuthorizeDict = dbFunctionAuthorizes.ToDictionary(t => t.FunctionId, t => t);

        foreach (var functionId in functionIdList)
        {
            if (!functionAuthorizeDict.TryGetValue(functionId, out var functionAuthorize))
            {
                functionAuthorize = new FunctionAuthorize
                {
                    Id = idService.GetId(),
                    FunctionId = functionId,
                    RoleId = roleId,
                    CreateUserId = userId
                };

                db.FunctionAuthorize.Add(functionAuthorize);
            }

            functionAuthorize.FunctionId = functionId;
            functionAuthorize.RoleId = roleId;
            functionAuthorize.UserId = null;
            functionAuthorize.DeleteTime = null;
            functionAuthorize.DeleteUserId = null;
        }

        await db.SaveChangesAsync();
    }


}


public class AuthorizeSeedData
{
    public AdminUserSeedItem AdminUser { get; set; } = new();

    public AdminRoleSeedItem AdminRole { get; set; } = new();

    public List<FunctionSeedItem> Functions { get; set; } = [];

    public List<FunctionRouteSeedItem> FunctionRoutes { get; set; } = [];
}


public class AdminUserSeedItem
{
    public string Name { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string Password { get; set; } = string.Empty;
}


public class AdminRoleSeedItem
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Remarks { get; set; }
}


public class FunctionSeedItem
{
    public string Name { get; set; } = string.Empty;

    public string Sign { get; set; } = string.Empty;

    public string? Remarks { get; set; }

    public string? ParentSign { get; set; }

    public int Type { get; set; }
}


public class FunctionRouteSeedItem
{
    public string Module { get; set; } = string.Empty;

    public string Route { get; set; } = string.Empty;

    public string? Remarks { get; set; }

    public string? FunctionSign { get; set; }
}
