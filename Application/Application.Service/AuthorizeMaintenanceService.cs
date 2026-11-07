using Application.Interface;
using Application.Model.Authorize;
using IdentifierGenerator;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository;
using Repository.Database;
using Repository.Database.Enums;
using SourceGenerator.Runtime.Attributes;
using System.Text;

namespace Application.Service;

/// <summary>
/// 维护权限路由及管理员初始化数据
/// </summary>
[RegisterService(typeof(IAuthorizeMaintenanceService), Lifetime = ServiceLifetime.Scoped)]
public class AuthorizeMaintenanceService(DatabaseContext db, IdService idService) : IAuthorizeMaintenanceService
{

    /// <summary>
    /// 同步宿主的鉴权路由并软删除已移除的路由
    /// </summary>
    public async Task UpdateRoutesAsync(string module, IReadOnlyCollection<AuthorizeRouteDto> routes)
    {

        var functionRoutes = await db.FunctionRoute.Where(t => t.Module == module).ToListAsync();
        var routeNames = routes.Select(t => t.Route).ToHashSet();

        foreach (var item in functionRoutes.Where(t => !routeNames.Contains(t.Route)))
        {
            item.DeleteTime = DateTimeOffset.UtcNow;
        }

        foreach (var route in routes)
        {
            var info = functionRoutes.FirstOrDefault(t => t.Route == route.Route);
            if (info != null)
            {
                info.Remark = route.Remark;
            }
            else
            {
                db.FunctionRoute.Add(new FunctionRoute
                {
                    Id = idService.GetId(),
                    Module = module,
                    Route = route.Route,
                    Remark = route.Remark
                });
            }
        }

        await db.SaveChangesAsync();

    }


    /// <summary>
    /// 事务内应用管理员、角色及权限配置
    /// </summary>
    public async Task<AuthorizeInitResultDto> InitializeAsync(AuthorizeSeedData seedData)
    {

        await using var transaction = await db.Database.BeginTransactionAsync();

        var adminUser = await UpsertAdminUserAsync(seedData.AdminUser);
        var adminRole = await UpsertAdminRoleAsync(seedData.AdminRole);
        var functionDict = await UpsertFunctionsAsync(seedData.Functions);
        await UpsertFunctionRoutesAsync(seedData.FunctionRoutes, functionDict);
        await EnsureAdminUserRoleAsync(adminUser.Id, adminRole.Id);
        await EnsureAdminRoleFunctionAuthorizeAsync(adminUser.Id, adminRole.Id);

        await transaction.CommitAsync();

        return new AuthorizeInitResultDto
        {
            AdminUser = adminUser.UserName,
            AdminRole = adminRole.Code,
            FunctionCount = seedData.Functions.Count,
            FunctionRouteCount = seedData.FunctionRoutes.Count,
            FunctionAuthorizeCount = await db.FunctionAuthorize.Where(t => t.RoleId == adminRole.Id && t.UserId == null).CountAsync(),
            UserRoleCount = await db.UserRole.Where(t => t.UserId == adminUser.Id).CountAsync()
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
        role.Remark = adminRole.Remark;
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
            function.Remark = item.Remark;
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
            functionRoute.Remark = item.Remark;
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
