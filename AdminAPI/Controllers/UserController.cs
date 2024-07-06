﻿using AdminAPI.Services;
using AdminShared.Models;
using AdminShared.Models.User;
using Common;
using DistributedLock;
using IdentifierGenerator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Repository.Database;
using System.Text;
using WebAPIBasic.Filters;
using WebAPIBasic.Libraries;

namespace AdminAPI.Controllers
{


    /// <summary>
    /// 用户数据操作控制器
    /// </summary>
    [SignVerifyFilter]
    [Route("[controller]/[action]")]
    [Authorize]
    [ApiController]
    public class UserController : ControllerBase
    {

        private readonly DatabaseContext db;
        private readonly IDistributedLock distLock;
        private readonly IdService idService;

        private readonly UserService userService;
        private readonly long userId;


        public UserController(DatabaseContext db, IDistributedLock distLock, IdService idService, UserService userService, IHttpContextAccessor httpContextAccessor)
        {
            this.db = db;
            this.distLock = distLock;
            this.idService = idService;
            this.userService = userService;

            var userIdStr = httpContextAccessor.HttpContext?.GetClaimByUser("userId");
            if (userIdStr != null)
            {
                userId = long.Parse(userIdStr);
            }
        }



        /// <summary>
        /// 获取用户列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpGet]
        public DtoPageList<DtoUser> GetUserList([FromQuery] DtoPageRequest request)
        {
            DtoPageList<DtoUser> data = new();

            var query = db.TUser.AsSplitQuery();

            data.Total = query.Count();

            data.List = query.OrderByDescending(t => t.CreateTime).Select(t => new DtoUser
            {
                Id = t.Id,
                Name = t.Name,
                UserName = t.UserName,
                Phone = t.Phone,
                Email = t.Email,
                Roles = string.Join("、", db.TUserRole.Where(r => r.UserId == t.Id).Select(r => r.Role.Name).ToList()),
                RoleIds = db.TUserRole.Where(r => r.UserId == t.Id).Select(r => r.Role.Id.ToString()).ToArray(),
                CreateTime = t.CreateTime
            }).Skip(request.Skip()).Take(request.PageSize).ToList();

            return data;
        }



        /// <summary>
        /// 通过 UserId 获取用户信息 
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns></returns>
        [HttpGet]
        public DtoUser? GetUser(long? userId)
        {

            userId ??= this.userId;

            var user = db.TUser.Where(t => t.Id == userId).Select(t => new DtoUser
            {
                Id = t.Id,
                Name = t.Name,
                UserName = t.UserName,
                Phone = t.Phone,
                Email = t.Email,
                Roles = string.Join(",", db.TUserRole.Where(r => r.UserId == t.Id).Select(r => r.Role.Name).ToList()),
                CreateTime = t.CreateTime
            }).FirstOrDefault();

            return user;
        }



        /// <summary>
        /// 创建用户
        /// </summary>
        /// <param name="createUser"></param>
        /// <returns></returns>
        [HttpPost]
        public long? CreateUser(DtoEditUser createUser)
        {
            string key = "userName:" + createUser.UserName.ToLower();

            using (var handle = distLock.TryLock(key))
            {
                if (handle != null)
                {
                    var isHaveUserName = db.TUser.Where(t => t.UserName == createUser.UserName).Any();

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
                                CreateUserId = this.userId,
                                RoleId = item
                            };

                            db.TUserRole.Add(userRole);
                        }

                        db.SaveChanges();

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
        [HttpPost]
        public bool UpdateUser(long userId, DtoEditUser updateUser)
        {
            string key = "userName:" + updateUser.UserName.ToLower();

            using (var handle = distLock.TryLock(key))
            {
                if (handle != null)
                {
                    var isHaveUserName = db.TUser.Where(t => t.Id != userId && t.UserName == updateUser.UserName).Any();

                    if (isHaveUserName)
                    {
                        var roleIds = updateUser.RoleIds.Select(t => long.Parse(t)).ToList();

                        var user = db.TUser.Where(t => t.Id == userId).FirstOrDefault();

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

                            var roleList = db.TUserRole.Where(t => t.UserId == user.Id).ToList();

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


                            db.SaveChanges();

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



        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        public bool DeleteUser(long id)
        {
            var user = db.TUser.Where(t => t.Id == id).FirstOrDefault();

            if (user != null)
            {
                user.IsDelete = true;
                user.DeleteUserId = userId;

                db.SaveChanges();

                return true;
            }
            else
            {
                return false;
            }

        }



        /// <summary>
        /// 获取某个用户的功能权限
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns></returns>
        [HttpGet]
        public List<DtoUserFunction> GetUserFunction(long userId)
        {
            var roleIds = db.TUserRole.Where(t => t.UserId == userId).Select(t => t.RoleId).ToList();

            var functionList = db.TFunction.Where(t => t.ParentId == null && t.Type == TFunction.EnumType.模块).Select(t => new DtoUserFunction
            {
                Id = t.Id,
                Name = t.Name.Replace(t.Parent!.Name + "-", ""),
                Sign = t.Sign,
                IsCheck = db.TFunctionAuthorize.Where(r => r.FunctionId == t.Id && (roleIds.Contains(r.RoleId!.Value) || r.UserId == userId)).FirstOrDefault() != null,
                FunctionList = db.TFunction.Where(f => f.ParentId == t.Id && f.Type == TFunction.EnumType.功能).Select(f => new DtoUserFunction
                {
                    Id = f.Id,
                    Name = f.Name.Replace(f.Parent!.Name + "-", ""),
                    Sign = f.Sign,
                    IsCheck = db.TFunctionAuthorize.Where(r => r.FunctionId == f.Id && (roleIds.Contains(r.RoleId!.Value) || r.UserId == userId)).FirstOrDefault() != null,
                }).ToList()
            }).ToList();

            foreach (var function in functionList)
            {
                function.ChildList = userService.GetUserFunctionChildList(userId, function.Id, roleIds);
            }

            return functionList;
        }




        /// <summary>
        /// 设置用户的功能
        /// </summary>
        /// <param name="setUserFunction"></param>
        /// <returns></returns>
        [QueueLimitFilter()]
        [HttpPost]
        public bool SetUserFunction(DtoSetUserFunction setUserFunction)
        {

            var roleIds = db.TUserRole.Where(t => t.UserId == setUserFunction.UserId).Select(t => t.RoleId).ToList();

            var functionAuthorize = db.TFunctionAuthorize.Where(t => (roleIds.Contains(t.RoleId!.Value) || t.UserId == setUserFunction.UserId) && t.FunctionId == setUserFunction.FunctionId).FirstOrDefault() ?? new TFunctionAuthorize();

            if (setUserFunction.IsCheck)
            {
                if (functionAuthorize.Id == default)
                {
                    functionAuthorize.Id = idService.GetId();
                    functionAuthorize.CreateUserId = userId;

                    functionAuthorize.FunctionId = setUserFunction.FunctionId;
                    functionAuthorize.UserId = setUserFunction.UserId;

                    db.TFunctionAuthorize.Add(functionAuthorize);

                    db.SaveChanges();
                }
            }
            else
            {
                if (functionAuthorize.Id != default)
                {
                    if (functionAuthorize.RoleId == null)
                    {
                        var userFunctionList = db.TFunctionAuthorize.Where(t => t.UserId == setUserFunction.UserId).ToList();

                        foreach (var userFunction in userFunctionList)
                        {
                            userFunction.IsDelete = true;
                            userFunction.DeleteUserId = userId;
                        }

                        db.SaveChanges();

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
        [HttpGet]
        public List<DtoUserRole> GetUserRoleList(long userId)
        {
            var list = db.TRole.Select(t => new DtoUserRole
            {
                Id = t.Id,
                Name = t.Name,
                Remarks = t.Remarks,
                IsCheck = db.TUserRole.Where(r => r.RoleId == t.Id && r.UserId == userId).FirstOrDefault() != null
            }).ToList();

            return list;
        }




        /// <summary>
        /// 设置用户角色
        /// </summary>
        /// <param name="setUserRole"></param>
        /// <returns></returns>
        [QueueLimitFilter()]
        [HttpPost]
        public bool SetUserRole(DtoSetUserRole setUserRole)
        {
            var userRole = db.TUserRole.Where(t => t.RoleId == setUserRole.RoleId && t.UserId == setUserRole.UserId).FirstOrDefault();

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

                    db.SaveChanges();
                }
            }
            else
            {
                if (userRole != null)
                {
                    userRole.IsDelete = true;
                    userRole.DeleteUserId = userId;

                    db.SaveChanges();
                }
            }

            return true;

        }



    }
}