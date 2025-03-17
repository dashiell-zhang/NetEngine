using Authorize.Interface;
using Common;
using DistributedLock;
using IdentifierGenerator;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using Repository.Enum;
using Shared.Model;
using System.Text;
using User.Interface;
using User.Model.User;

namespace User.Service
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class UserService(DatabaseContext db, IDistributedCache distributedCache, IUserContext userContext, IDistributedLock distLock, IdService idService) : IUserService
    {

        private long userId => userContext.UserId;


        public Task<DtoUser?> GetUserAsync(long? userId)
        {
            userId ??= this.userId;

            var user = db.TUser.Where(t => t.Id == userId).Select(t => new DtoUser
            {
                Name = t.Name,
                UserName = t.UserName,
                Phone = t.Phone,
                Email = t.Email,
                Roles = string.Join(",", db.TUserRole.Where(r => r.UserId == t.Id).Select(r => r.Role.Code).ToList()),
                CreateTime = t.CreateTime
            }).FirstOrDefaultAsync();

            return user;
        }


        public async Task<bool> EditUserPhoneBySmsAsync(DtoEditUserPhoneBySms request)
        {
            string key = "VerifyPhone_" + request.NewPhone;

            var code = await distributedCache.GetStringAsync(key);

            if (string.IsNullOrEmpty(code) == false && code == request.SmsCode)
            {
                var user = await db.TUser.Where(t => t.Id == userId).FirstOrDefaultAsync();

                if (user != null)
                {
                    var checkPhone = await db.TUser.Where(t => t.Id != userId && t.Phone == request.NewPhone).CountAsync();

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


        public async Task<DtoPageList<DtoUser>> GetUserListAsync(DtoPageRequest request)
        {
            DtoPageList<DtoUser> result = new();

            var query = db.TUser.AsSplitQuery();

            result.Total = await query.CountAsync();

            result.List = await query.OrderByDescending(t => t.CreateTime).Select(t => new DtoUser
            {
                Id = t.Id,
                Name = t.Name,
                UserName = t.UserName,
                Phone = t.Phone,
                Email = t.Email,
                Roles = string.Join("、", db.TUserRole.Where(r => r.UserId == t.Id).Select(r => r.Role.Code).ToList()),
                RoleIds = db.TUserRole.Where(r => r.UserId == t.Id).Select(r => r.Role.Id.ToString()).ToArray(),
                CreateTime = t.CreateTime
            }).Skip(request.Skip()).Take(request.PageSize).ToListAsync();


            return result;
        }


        public async Task<long?> CreateUserAsync(DtoEditUser createUser)
        {
            string key = "userName:" + createUser.UserName.ToLower();

            using (var handle = await distLock.TryLockAsync(key))
            {
                if (handle != null)
                {
                    var isHaveUserName = await db.TUser.Where(t => t.UserName == createUser.UserName).AnyAsync();

                    if (isHaveUserName == false)
                    {
                        var roleIds = createUser.RoleIds.Select(t => long.Parse(t)).ToList();

                        TUser user = new()
                        {
                            Id = idService.GetId(),
                            Name = createUser.Name,
                            UserName = createUser.UserName,
                            Phone = createUser.Phone
                        };
                        user.Password = Convert.ToBase64String(KeyDerivation.Pbkdf2(createUser.Password, Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));
                        user.CreateUserId = userId;

                        user.Email = createUser.Email;
                        db.TUser.Add(user);

                        foreach (var item in roleIds)
                        {
                            TUserRole userRole = new()
                            {
                                Id = idService.GetId(),
                                UserId = user.Id,
                                CreateUserId = userId,
                                RoleId = item
                            };

                            db.TUserRole.Add(userRole);
                        }

                        await db.SaveChangesAsync();

                        return user.Id;
                    }
                }
            }
            throw new CustomException("用户名已被占用,无法保存");

        }


        public async Task<bool> UpdateUserAsync(long userId, DtoEditUser updateUser)
        {
            string key = "userName:" + updateUser.UserName.ToLower();

            using (var handle = await distLock.TryLockAsync(key))
            {
                if (handle != null)
                {
                    var isHaveUserName = await db.TUser.Where(t => t.Id != userId && t.UserName == updateUser.UserName).AnyAsync();

                    if (!isHaveUserName)
                    {
                        var roleIds = updateUser.RoleIds.Select(t => long.Parse(t)).ToList();

                        var user = await db.TUser.Where(t => t.Id == userId).FirstOrDefaultAsync();

                        if (user != null)
                        {
                            user.UpdateUserId = this.userId;

                            user.Name = updateUser.Name;
                            user.UserName = updateUser.UserName;
                            user.Phone = updateUser.Phone;
                            user.Email = updateUser.Email;

                            if (updateUser.Password != "default")
                            {
                                user.Password = Convert.ToBase64String(KeyDerivation.Pbkdf2(updateUser.Password, Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));
                            }

                            var roleList = await db.TUserRole.Where(t => t.UserId == user.Id).ToListAsync();

                            foreach (var item in roleList)
                            {
                                if (roleIds.Contains(item.RoleId))
                                {
                                    roleIds.Remove(item.RoleId);
                                }
                                else
                                {
                                    item.IsDelete = true;
                                    item.DeleteUserId = this.userId;
                                }
                            }

                            foreach (var item in roleIds)
                            {
                                TUserRole userRole = new()
                                {
                                    Id = idService.GetId(),
                                    UserId = userId,
                                    CreateUserId = this.userId,
                                    RoleId = item
                                };

                                db.TUserRole.Add(userRole);
                            }

                            await db.SaveChangesAsync();

                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }

            throw new CustomException("用户名已被占用,无法保存");
        }


        public async Task<bool> DeleteUserAsync(long id)
        {
            var user = await db.TUser.Where(t => t.Id == id).FirstOrDefaultAsync();

            if (user != null)
            {
                user.IsDelete = true;
                user.DeleteUserId = userId;

                await db.SaveChangesAsync();

                return true;
            }
            else
            {
                return false;
            }
        }


        public async Task<List<DtoUserFunction>> GetUserFunctionAsync(long userId)
        {
            var roleIds = await db.TUserRole.Where(t => t.UserId == userId).Select(t => t.RoleId).ToListAsync();

            var functionList = await db.TFunction.Where(t => t.ParentId == null && t.Type == EnumFunctionType.Module).Select(t => new DtoUserFunction
            {
                Id = t.Id,
                Name = t.Name.Replace(t.Parent!.Name + "-", ""),
                Sign = t.Sign,
                IsCheck = db.TFunctionAuthorize.Where(r => r.FunctionId == t.Id && (roleIds.Contains(r.RoleId!.Value) || r.UserId == userId)).FirstOrDefault() != null,
                FunctionList = db.TFunction.Where(f => f.ParentId == t.Id && f.Type == EnumFunctionType.Function).Select(f => new DtoUserFunction
                {
                    Id = f.Id,
                    Name = f.Name.Replace(f.Parent!.Name + "-", ""),
                    Sign = f.Sign,
                    IsCheck = db.TFunctionAuthorize.Where(r => r.FunctionId == f.Id && (roleIds.Contains(r.RoleId!.Value) || r.UserId == userId)).FirstOrDefault() != null,
                }).ToList()
            }).ToListAsync();

            foreach (var function in functionList)
            {
                function.ChildList = await GetUserFunctionChildListAsync(userId, function.Id, roleIds);
            }

            return functionList;
        }


        public async Task<bool> SetUserFunctionAsync(DtoSetUserFunction setUserFunction)
        {
            var roleIds = await db.TUserRole.Where(t => t.UserId == setUserFunction.UserId).Select(t => t.RoleId).ToListAsync();

            var functionAuthorize = await db.TFunctionAuthorize.Where(t => (roleIds.Contains(t.RoleId!.Value) || t.UserId == setUserFunction.UserId) && t.FunctionId == setUserFunction.FunctionId).FirstOrDefaultAsync() ?? new TFunctionAuthorize();

            if (setUserFunction.IsCheck)
            {
                if (functionAuthorize.Id == default)
                {
                    functionAuthorize.Id = idService.GetId();
                    functionAuthorize.CreateUserId = userId;

                    functionAuthorize.FunctionId = setUserFunction.FunctionId;
                    functionAuthorize.UserId = setUserFunction.UserId;

                    db.TFunctionAuthorize.Add(functionAuthorize);

                    await db.SaveChangesAsync();
                }
            }
            else
            {
                if (functionAuthorize.Id != default)
                {
                    if (functionAuthorize.RoleId == null)
                    {
                        var userFunctionList = await db.TFunctionAuthorize.Where(t => t.UserId == setUserFunction.UserId).ToListAsync();

                        foreach (var userFunction in userFunctionList)
                        {
                            userFunction.IsDelete = true;
                            userFunction.DeleteUserId = userId;
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


        public Task<List<DtoUserRole>> GetUserRoleListAsync(long userId)
        {
            var list = db.TRole.Select(t => new DtoUserRole
            {
                Id = t.Id,
                Name = t.Name,
                Remarks = t.Remarks,
                IsCheck = db.TUserRole.Where(r => r.RoleId == t.Id && r.UserId == userId).FirstOrDefault() != null
            }).ToListAsync();

            return list;
        }


        public async Task<bool> SetUserRoleAsync(DtoSetUserRole setUserRole)
        {
            var userRole = await db.TUserRole.Where(t => t.RoleId == setUserRole.RoleId && t.UserId == setUserRole.UserId).FirstOrDefaultAsync();

            if (setUserRole.IsCheck)
            {
                if (userRole == null)
                {
                    userRole = new TUserRole
                    {
                        Id = idService.GetId(),
                        CreateUserId = userId,
                        UserId = setUserRole.UserId,
                        RoleId = setUserRole.RoleId
                    };

                    db.TUserRole.Add(userRole);

                    await db.SaveChangesAsync();
                }
            }
            else
            {
                if (userRole != null)
                {
                    userRole.IsDelete = true;
                    userRole.DeleteUserId = userId;

                    await db.SaveChangesAsync();
                }
            }

            return true;

        }


        public async Task<List<DtoUserFunction>> GetUserFunctionChildListAsync(long userId, long parentId, List<long> roleIds)
        {

            var functionList = await db.TFunction.Where(t => t.ParentId == parentId && t.Type == EnumFunctionType.Module).Select(t => new DtoUserFunction
            {
                Id = t.Id,
                Name = t.Name.Replace(t.Parent!.Name + "-", ""),
                Sign = t.Sign,
                IsCheck = db.TFunctionAuthorize.Where(r => r.FunctionId == t.Id && (roleIds.Contains(r.RoleId!.Value) || r.UserId == userId)).FirstOrDefault() != null,
                FunctionList = db.TFunction.Where(f => f.ParentId == t.Id && f.Type == EnumFunctionType.Function).Select(f => new DtoUserFunction
                {
                    Id = f.Id,
                    Name = f.Name.Replace(f.Parent!.Name + "-", ""),
                    Sign = f.Sign,
                    IsCheck = db.TFunctionAuthorize.Where(r => r.FunctionId == f.Id && (roleIds.Contains(r.RoleId!.Value) || r.UserId == userId)).FirstOrDefault() != null,
                }).ToList()
            }).ToListAsync();

            foreach (var function in functionList)
            {
                function.ChildList = await GetUserFunctionChildListAsync(userId, function.Id, roleIds);
            }

            return functionList;
        }

    }
}
