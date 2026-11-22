using Application.Interface.User;
using Application.Model.Shared;
using Application.Model.User.User;
using Common;
using DistributedLock;
using IdentifierGenerator;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Repository;
using Repository.Database;
using SourceGenerator.Runtime.Attributes;
using System.Globalization;
using System.Text;

namespace Application.Service.User;

/// <summary>
/// 提供用户资料、角色和功能权限管理能力
/// </summary>
[RegisterService(typeof(IUserService), Lifetime = ServiceLifetime.Scoped)]
public class UserService(DatabaseContext db, IDistributedCache distributedCache, IDistributedLock distLock, IdService idService) : IUserService
{


    /// <summary>
    /// 通过 UserId 获取用户信息 
    /// </summary>
    /// <param name="targetUserId">目标用户ID</param>
    /// <returns></returns>
    public Task<UserDto?> GetUserAsync(long targetUserId)
    {
        var user = db.User.Where(t => t.Id == targetUserId).Select(t => new UserDto
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
    /// <param name="actorUserId">行为发起人用户ID</param>
    /// <param name="request">手机号修改请求</param>
    /// <returns></returns>
    public async Task<bool> EditUserPhoneBySmsAsync(long actorUserId, EditUserPhoneBySmsDto request)
    {
        string key = "VerifyPhone_" + request.NewPhone;

        var code = await distributedCache.GetStringAsync(key);

        if (string.IsNullOrEmpty(code) == false && code == request.SmsCode)
        {
            var user = await db.User.Where(t => t.Id == actorUserId).FirstOrDefaultAsync();

            if (user != null)
            {
                var checkPhone = await db.User.Where(t => t.Id != actorUserId && t.Phone == request.NewPhone).CountAsync();

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
    public async Task<PageListDto<UserDto>> GetUserListAsync(UserPageRequestDto request)
    {
        PageListDto<UserDto> result = new();

        var skip = request.Skip();
        var query = db.User.AsQueryable();

        var keyword = request.Keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(t =>
                t.Name.Contains(keyword) ||
                t.UserName.Contains(keyword) ||
                t.Phone.Contains(keyword) ||
                t.Email!.Contains(keyword));
        }

        result.Total = await query.CountAsync();
        if (result.Total == 0)
        {
            return result;
        }

        if (skip >= result.Total)
        {
            result.List = [];
            return result;
        }

        var users = await query.OrderByDescending(t => t.Id)
            .Skip(skip).Take(request.PageSize)
            .Select(t => new UserDto
            {
                Id = t.Id,
                Name = t.Name,
                UserName = t.UserName,
                Phone = t.Phone,
                Email = t.Email,
                CreateTime = t.CreateTime
            })
            .ToListAsync();
        result.List = users;
        if (users.Count == 0)
        {
            return result;
        }

        var userIds = users.Select(t => t.Id).ToArray();
        var assignments = await db.UserRole.Where(t => userIds.Contains(t.UserId))
            .OrderBy(t => t.Id)
            .Select(t => new { t.UserId, RoleId = t.Role.Id, t.Role.Code })
            .ToListAsync();
        var rolesByUser = assignments.ToLookup(t => t.UserId);
        foreach (var user in users)
        {
            var roles = rolesByUser[user.Id];
            user.Roles = string.Join("、", roles.Select(t => t.Code));
            user.RoleIds = roles.Select(t => t.RoleId.ToString(CultureInfo.InvariantCulture)).ToArray();
        }

        return result;
    }


    /// <summary>
    /// 创建用户
    /// </summary>
    /// <param name="actorUserId">行为发起人用户ID</param>
    /// <param name="createUser"></param>
    /// <returns></returns>
    public async Task<long?> CreateUserAsync(long actorUserId, EditUserDto createUser)
    {
        string key = "userName:" + createUser.UserName.ToLower();

        await using (var handle = await distLock.TryLockAsync(key))
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
                    user.CreateUserId = actorUserId;

                    user.Email = createUser.Email;
                    db.User.Add(user);

                    foreach (var item in roleIds)
                    {
                        UserRole userRole = new()
                        {
                            Id = idService.GetId(),
                            UserId = user.Id,
                            CreateUserId = actorUserId,
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
    /// <param name="actorUserId">行为发起人用户ID</param>
    /// <param name="targetUserId">目标用户ID</param>
    /// <param name="updateUser"></param>
    /// <returns></returns>
    public async Task<bool> UpdateUserAsync(long actorUserId, long targetUserId, EditUserDto updateUser)
    {
        string key = "userName:" + updateUser.UserName.ToLower();

        await using (var handle = await distLock.TryLockAsync(key))
        {
            if (handle != null)
            {
                var isHaveUserName = await db.User.Where(t => t.Id != targetUserId && t.UserName == updateUser.UserName).AnyAsync();

                if (!isHaveUserName)
                {
                    var roleIds = updateUser.RoleIds.Select(t => long.Parse(t)).ToList();

                    var user = await db.User.Where(t => t.Id == targetUserId).FirstOrDefaultAsync();

                    if (user != null)
                    {
                        user.UpdateUserId = actorUserId;

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
                                item.DeleteTime = DateTimeOffset.UtcNow;
                                item.DeleteUserId = actorUserId;
                            }
                        }

                        foreach (var item in roleIds)
                        {
                            UserRole userRole = new()
                            {
                                Id = idService.GetId(),
                                UserId = targetUserId,
                                CreateUserId = actorUserId,
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
    /// <param name="actorUserId">行为发起人用户ID</param>
    /// <param name="targetUserId">目标用户ID</param>
    /// <returns></returns>
    public async Task<bool> DeleteUserAsync(long actorUserId, long targetUserId)
    {
        var user = await db.User.Where(t => t.Id == targetUserId).FirstOrDefaultAsync();

        if (user != null)
        {
            user.DeleteTime = DateTimeOffset.UtcNow;
            user.DeleteUserId = actorUserId;

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
        return await LoadUserFunctionTreeAsync(userId, null, roleIds);

    }


    /// <summary>
    /// 设置用户的功能
    /// </summary>
    /// <param name="actorUserId">行为发起人用户ID</param>
    /// <param name="setUserFunction"></param>
    /// <returns></returns>
    public async Task<bool> SetUserFunctionAsync(long actorUserId, SetUserFunctionDto setUserFunction)
    {
        var roleIds = await db.UserRole.Where(t => t.UserId == setUserFunction.UserId).Select(t => t.RoleId).ToListAsync();

        var functionAuthorize = await db.FunctionAuthorize.Where(t => (roleIds.Contains(t.RoleId!.Value) || t.UserId == setUserFunction.UserId) && t.FunctionId == setUserFunction.FunctionId).FirstOrDefaultAsync() ?? new FunctionAuthorize();

        if (setUserFunction.IsCheck)
        {
            if (functionAuthorize.Id == default)
            {
                functionAuthorize.Id = idService.GetId();
                functionAuthorize.CreateUserId = actorUserId;

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
                    functionAuthorize.DeleteTime = DateTimeOffset.UtcNow;
                    functionAuthorize.DeleteUserId = actorUserId;

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
            Remark = t.Remark,
            IsCheck = db.UserRole.Where(r => r.RoleId == t.Id && r.UserId == userId).FirstOrDefault() != null
        }).ToListAsync();

        return list;
    }


    /// <summary>
    /// 设置用户角色
    /// </summary>
    /// <param name="actorUserId">行为发起人用户ID</param>
    /// <param name="setUserRole"></param>
    /// <returns></returns>
    public async Task<bool> SetUserRoleAsync(long actorUserId, SetUserRoleDto setUserRole)
    {
        var userRole = await db.UserRole.Where(t => t.RoleId == setUserRole.RoleId && t.UserId == setUserRole.UserId).FirstOrDefaultAsync();

        if (setUserRole.IsCheck)
        {
            if (userRole == null)
            {
                userRole = new UserRole
                {
                    Id = idService.GetId(),
                    CreateUserId = actorUserId,
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
                userRole.DeleteTime = DateTimeOffset.UtcNow;
                userRole.DeleteUserId = actorUserId;

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
    public Task<List<UserFunctionDto>> GetUserFunctionChildListAsync(long userId, long parentId, List<long> roleIds) => LoadUserFunctionTreeAsync(userId, parentId, roleIds);


    /// <summary>
    /// 批量读取用户的模块树及直接和继承授权
    /// </summary>
    private Task<List<UserFunctionDto>> LoadUserFunctionTreeAsync(long userId, long? parentId, List<long> roleIds)
    {

        var grants = db.FunctionAuthorize.Where(t => roleIds.Contains(t.RoleId!.Value) || t.UserId == userId)
            .Select(t => t.FunctionId);
        return FunctionTree.LoadAsync(db, parentId, grants,
            (id, name, sign, isCheck) => new UserFunctionDto { Id = id, Name = name, Sign = sign, IsCheck = isCheck },
            (node, children, functions) =>
            {
                node.ChildList = children;
                node.FunctionList = functions;
            });

    }

}
