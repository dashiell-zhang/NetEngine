using Application.Interface;
using Application.Model.Shared;
using Application.Model.User.User;
using Common;
using DistributedLock;
using IdentifierGenerator;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using Repository.Enum;
using SourceGenerator.Runtime.Attributes;
using System.Text;

namespace Application.Service.User;
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class UserService(DatabaseContext db, IDistributedCache distributedCache, IUserContext userContext, IDistributedLock distLock, IdService idService)
{

    private long UserId => userContext.UserId;


    /// <summary>
    /// 通过 UserId 获取用户信息 
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns></returns>
    public Task<UserDto?> GetUserAsync(long? userId)
    {
        userId ??= UserId;

        var user = db.User.Where(t => t.Id == userId).Select(t => new UserDto
        {
            Id = t.Id,
            Name = t.Name,
            UserName = t.UserName,
            Phone = t.Phone,
            Email = t.Email,
            Roles = string.Join(",", db.UserRole.Where(r => r.UserId == t.Id).Select(r => r.Role.Code).ToList()),
            CreateTime = t.CreateTime
        }).FirstOrDefaultAsync();

        return user;
    }


    /// <summary>
    /// 通过短信验证码修改账户手机号
    /// </summary>
    /// <param name="keyValue">key 为新手机号，value 为短信验证码</param>
    /// <returns></returns>
    public async Task<bool> EditUserPhoneBySmsAsync(EditUserPhoneBySmsDto request)
    {
        string key = "VerifyPhone_" + request.NewPhone;

        var code = await distributedCache.GetStringAsync(key);

        if (string.IsNullOrEmpty(code) == false && code == request.SmsCode)
        {
            var user = await db.User.Where(t => t.Id == UserId).FirstOrDefaultAsync();

            if (user != null)
            {
                var checkPhone = await db.User.Where(t => t.Id != UserId && t.Phone == request.NewPhone).CountAsync();

                if (checkPhone == 0)
                {
                    user.Phone = request.NewPhone;

                    await db.SaveChangesAsync();

                    return true;
                }
                else
                {
                    throw new CustomException("手机号已被其他账户绑定");
                }
            }
            else
            {
                throw new CustomException("账户不存在");
            }
        }
        else
        {
            throw new CustomException("短信验证码错误");
        }
    }


    /// <summary>
    /// 获取用户列表
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<PageListDto<UserDto>> GetUserListAsync(PageRequestDto request)
    {
        PageListDto<UserDto> result = new();

        var query = db.User.AsSplitQuery();

        result.Total = await query.CountAsync();

        if (result.Total != 0)
        {
            result.List = await query.OrderByDescending(t => t.Id).Select(t => new UserDto
            {
                Id = t.Id,
                Name = t.Name,
                UserName = t.UserName,
                Phone = t.Phone,
                Email = t.Email,
                Roles = string.Join("、", db.UserRole.Where(r => r.UserId == t.Id).Select(r => r.Role.Code).ToList()),
                RoleIds = db.UserRole.Where(r => r.UserId == t.Id).Select(r => r.Role.Id.ToString()).ToArray(),
                CreateTime = t.CreateTime
            }).Skip(request.Skip()).Take(request.PageSize).ToListAsync();
        }

        return result;
    }


    /// <summary>
    /// 创建用户
    /// </summary>
    /// <param name="createUser"></param>
    /// <returns></returns>
    public async Task<long?> CreateUserAsync(EditUserDto createUser)
    {
        string key = "userName:" + createUser.UserName.ToLower();

        using (var handle = await distLock.TryLockAsync(key))
        {
            if (handle != null)
            {
                var isHaveUserName = await db.User.Where(t => t.UserName == createUser.UserName).AnyAsync();

                if (isHaveUserName == false)
                {
                    var roleIds = createUser.RoleIds.Select(t => long.Parse(t)).ToList();

                    Repository.Database.User user = new()
                    {
                        Id = idService.GetId(),
                        Name = createUser.Name,
                        UserName = createUser.UserName,
                        Phone = createUser.Phone
                    };
                    user.Password = Convert.ToBase64String(KeyDerivation.Pbkdf2(createUser.Password, Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));
                    user.CreateUserId = UserId;

                    user.Email = createUser.Email;
                    db.User.Add(user);

                    foreach (var item in roleIds)
                    {
                        UserRole userRole = new()
                        {
                            Id = idService.GetId(),
                            UserId = user.Id,
                            CreateUserId = UserId,
                            RoleId = item
                        };

                        db.UserRole.Add(userRole);
                    }

                    await db.SaveChangesAsync();

                    return user.Id;
                }
            }
        }
        throw new CustomException("用户名已被占用,无法保存");
    }


    /// <summary>
    /// 更新用户信息
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="updateUser"></param>
    /// <returns></returns>
    public async Task<bool> UpdateUserAsync(long userId, EditUserDto updateUser)
    {
        string key = "userName:" + updateUser.UserName.ToLower();

        using (var handle = await distLock.TryLockAsync(key))
        {
            if (handle != null)
            {
                var isHaveUserName = await db.User.Where(t => t.Id != userId && t.UserName == updateUser.UserName).AnyAsync();

                if (!isHaveUserName)
                {
                    var roleIds = updateUser.RoleIds.Select(t => long.Parse(t)).ToList();

                    var user = await db.User.Where(t => t.Id == userId).FirstOrDefaultAsync();

                    if (user != null)
                    {
                        user.UpdateUserId = UserId;

                        user.Name = updateUser.Name;
                        user.UserName = updateUser.UserName;
                        user.Phone = updateUser.Phone;
                        user.Email = updateUser.Email;

                        if (updateUser.Password != "default")
                        {
                            user.Password = Convert.ToBase64String(KeyDerivation.Pbkdf2(updateUser.Password, Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));
                        }

                        var roleList = await db.UserRole.Where(t => t.UserId == user.Id).ToListAsync();

                        foreach (var item in roleList)
                        {
                            if (roleIds.Contains(item.RoleId))
                            {
                                roleIds.Remove(item.RoleId);
                            }
                            else
                            {
                                item.IsDelete = true;
                                item.DeleteUserId = UserId;
                            }
                        }

                        foreach (var item in roleIds)
                        {
                            UserRole userRole = new()
                            {
                                Id = idService.GetId(),
                                UserId = userId,
                                CreateUserId = UserId,
                                RoleId = item
                            };

                            db.UserRole.Add(userRole);
                        }

                        await db.SaveChangesAsync();

                        return true;
                    }
                    else
                    {
                        throw new CustomException("无效的UserId");
                    }
                }
            }
        }

        throw new CustomException("用户名已被占用,无法保存");
    }


    /// <summary>
    /// 删除用户
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task<bool> DeleteUserAsync(long id)
    {
        var user = await db.User.Where(t => t.Id == id).FirstOrDefaultAsync();

        if (user != null)
        {
            user.IsDelete = true;
            user.DeleteUserId = UserId;

            await db.SaveChangesAsync();
        }

        return true;
    }


    /// <summary>
    /// 获取某个用户的功能权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns></returns>
    public async Task<List<UserFunctionDto>> GetUserFunctionAsync(long userId)
    {
        var roleIds = await db.UserRole.Where(t => t.UserId == userId).Select(t => t.RoleId).ToListAsync();

        var functionList = await db.Function.Where(t => t.ParentId == null && t.Type == EnumFunctionType.Module).Select(t => new UserFunctionDto
        {
            Id = t.Id,
            Name = t.Name.Replace(t.Parent!.Name + "-", ""),
            Sign = t.Sign,
            IsCheck = db.FunctionAuthorize.Where(r => r.FunctionId == t.Id && (roleIds.Contains(r.RoleId!.Value) || r.UserId == userId)).FirstOrDefault() != null,
            FunctionList = db.Function.Where(f => f.ParentId == t.Id && f.Type == EnumFunctionType.Function).Select(f => new UserFunctionDto
            {
                Id = f.Id,
                Name = f.Name.Replace(f.Parent!.Name + "-", ""),
                Sign = f.Sign,
                IsCheck = db.FunctionAuthorize.Where(r => r.FunctionId == f.Id && (roleIds.Contains(r.RoleId!.Value) || r.UserId == userId)).FirstOrDefault() != null,
            }).ToList()
        }).ToListAsync();

        foreach (var function in functionList)
        {
            function.ChildList = await GetUserFunctionChildListAsync(userId, function.Id, roleIds);
        }

        return functionList;
    }


    /// <summary>
    /// 设置用户的功能
    /// </summary>
    /// <param name="setUserFunction"></param>
    /// <returns></returns>
    public async Task<bool> SetUserFunctionAsync(SetUserFunctionDto setUserFunction)
    {
        var roleIds = await db.UserRole.Where(t => t.UserId == setUserFunction.UserId).Select(t => t.RoleId).ToListAsync();

        var functionAuthorize = await db.FunctionAuthorize.Where(t => (roleIds.Contains(t.RoleId!.Value) || t.UserId == setUserFunction.UserId) && t.FunctionId == setUserFunction.FunctionId).FirstOrDefaultAsync() ?? new FunctionAuthorize();

        if (setUserFunction.IsCheck)
        {
            if (functionAuthorize.Id == default)
            {
                functionAuthorize.Id = idService.GetId();
                functionAuthorize.CreateUserId = UserId;

                functionAuthorize.FunctionId = setUserFunction.FunctionId;
                functionAuthorize.UserId = setUserFunction.UserId;

                db.FunctionAuthorize.Add(functionAuthorize);

                await db.SaveChangesAsync();
            }
        }
        else
        {
            if (functionAuthorize.Id != default)
            {
                if (functionAuthorize.RoleId == null)
                {
                    var userFunctionList = await db.FunctionAuthorize.Where(t => t.UserId == setUserFunction.UserId).ToListAsync();

                    foreach (var userFunction in userFunctionList)
                    {
                        userFunction.IsDelete = true;
                        userFunction.DeleteUserId = UserId;
                    }

                    await db.SaveChangesAsync();

                    return true;
                }
                else
                {
                    throw new CustomException("该权限继承自角色，无法单独删除！");
                }
            }
        }

        return true;
    }


    /// <summary>
    /// 获取用户角色列表
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public Task<List<UserRoleDto>> GetUserRoleListAsync(long userId)
    {
        var list = db.Role.Select(t => new UserRoleDto
        {
            Id = t.Id,
            Name = t.Name,
            Remarks = t.Remarks,
            IsCheck = db.UserRole.Where(r => r.RoleId == t.Id && r.UserId == userId).FirstOrDefault() != null
        }).ToListAsync();

        return list;
    }


    /// <summary>
    /// 设置用户角色
    /// </summary>
    /// <param name="setUserRole"></param>
    /// <returns></returns>
    public async Task<bool> SetUserRoleAsync(SetUserRoleDto setUserRole)
    {
        var userRole = await db.UserRole.Where(t => t.RoleId == setUserRole.RoleId && t.UserId == setUserRole.UserId).FirstOrDefaultAsync();

        if (setUserRole.IsCheck)
        {
            if (userRole == null)
            {
                userRole = new UserRole
                {
                    Id = idService.GetId(),
                    CreateUserId = UserId,
                    UserId = setUserRole.UserId,
                    RoleId = setUserRole.RoleId
                };

                db.UserRole.Add(userRole);

                await db.SaveChangesAsync();
            }
        }
        else
        {
            if (userRole != null)
            {
                userRole.IsDelete = true;
                userRole.DeleteUserId = UserId;

                await db.SaveChangesAsync();
            }
        }

        return true;

    }


    /// <summary>
    /// 获取某个用户某个功能下的子集功能
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="parentId">功能父级ID</param>
    /// <param name="roleIds">用户角色ID集合</param>
    /// <returns></returns>
    public async Task<List<UserFunctionDto>> GetUserFunctionChildListAsync(long userId, long parentId, List<long> roleIds)
    {

        var functionList = await db.Function.Where(t => t.ParentId == parentId && t.Type == EnumFunctionType.Module).Select(t => new UserFunctionDto
        {
            Id = t.Id,
            Name = t.Name.Replace(t.Parent!.Name + "-", ""),
            Sign = t.Sign,
            IsCheck = db.FunctionAuthorize.Where(r => r.FunctionId == t.Id && (roleIds.Contains(r.RoleId!.Value) || r.UserId == userId)).FirstOrDefault() != null,
            FunctionList = db.Function.Where(f => f.ParentId == t.Id && f.Type == EnumFunctionType.Function).Select(f => new UserFunctionDto
            {
                Id = f.Id,
                Name = f.Name.Replace(f.Parent!.Name + "-", ""),
                Sign = f.Sign,
                IsCheck = db.FunctionAuthorize.Where(r => r.FunctionId == f.Id && (roleIds.Contains(r.RoleId!.Value) || r.UserId == userId)).FirstOrDefault() != null,
            }).ToList()
        }).ToListAsync();

        foreach (var function in functionList)
        {
            function.ChildList = await GetUserFunctionChildListAsync(userId, function.Id, roleIds);
        }

        return functionList;
    }

}
