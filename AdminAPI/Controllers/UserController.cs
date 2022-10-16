using AdminAPI.Filters;
using AdminAPI.Libraries;
using AdminAPI.Services;
using AdminShared.Models;
using AdminShared.Models.Role;
using AdminShared.Models.User;
using Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Logging;
using Repository.Database;
using System.Text;

namespace AdminAPI.Controllers
{


    /// <summary>
    /// 用户数据操作控制器
    /// </summary>
    [SignVerifyFilter]
    [Route("[controller]")]
    [Authorize]
    [ApiController]
    public class UserController : ControllerBase
    {

        private readonly DatabaseContext db;
        private readonly IDHelper idHelper;

        private readonly UserService userService;
        private readonly long userId;


        public UserController(DatabaseContext db, IDHelper idHelper, UserService userService, IHttpContextAccessor httpContextAccessor)
        {
            this.db = db;
            this.idHelper = idHelper;
            this.userService = userService;

            var userIdStr = httpContextAccessor.HttpContext?.GetClaimByAuthorization("userId");
            if (userIdStr != null)
            {
                userId = long.Parse(userIdStr);
            }
        }



        /// <summary>
        /// 获取用户列表
        /// </summary>
        /// <param name="pageNum"></param>
        /// <param name="pageSize"></param>
        /// <param name="searchKey"></param>
        /// <returns></returns>
        [HttpGet("GetUserList")]
        public DtoPageList<DtoUser> GetUserList(int pageNum, int pageSize, string? searchKey)
        {
            DtoPageList<DtoUser> data = new();

            int skip = (pageNum - 1) * pageSize;

            var query = db.TUser.Where(t => t.IsDelete == false);

            if (!string.IsNullOrEmpty(searchKey))
            {
                query = query.Where(t => t.Name.Contains(searchKey) || t.NickName.Contains(searchKey) || t.Phone.Contains(searchKey));
            }


            data.Total = query.Count();

            data.List = query.OrderByDescending(t => t.CreateTime).Select(t => new DtoUser
            {
                Id = t.Id,
                Name = t.Name,
                NickName = t.NickName,
                Phone = t.Phone,
                Email = t.Email,
                Roles = string.Join("、", db.TUserRole.Where(r => r.IsDelete == false && r.UserId == t.Id).Select(r => r.Role.Name).ToList()),
                RoleIds = db.TUserRole.Where(r => r.IsDelete == false && r.UserId == t.Id).Select(r => r.Role.Id.ToString()).ToArray(),
                CreateTime = t.CreateTime
            }).Skip(skip).Take(pageSize).ToList();

            return data;
        }



        /// <summary>
        /// 通过 UserId 获取用户信息 
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns></returns>
        [HttpGet("GetUser")]
        public DtoUser? GetUser(long? userId)
        {

            userId ??= this.userId;

            var user = db.TUser.Where(t => t.Id == userId && t.IsDelete == false).Select(t => new DtoUser
            {
                Id = t.Id,
                Name = t.Name,
                NickName = t.NickName,
                Phone = t.Phone,
                Email = t.Email,
                Roles = string.Join(",", db.TUserRole.Where(r => r.IsDelete == false && r.UserId == t.Id).Select(r => r.Role.Name).ToList()),
                CreateTime = t.CreateTime
            }).FirstOrDefault();

            return user;
        }



        /// <summary>
        /// 创建用户
        /// </summary>
        /// <param name="createUser"></param>
        /// <returns></returns>
        [HttpPost("CreateUser")]
        public long CreateUser(DtoEditUser createUser)
        {
            var roleIds = createUser.RoleIds.Select(t => long.Parse(t)).ToList();

            TUser user = new()
            {
                Id = idHelper.GetId(),
                Name = createUser.Name,
                NickName = createUser.NickName,
                Phone = createUser.Phone
            };
            user.PassWord = Convert.ToBase64String(KeyDerivation.Pbkdf2(createUser.PassWord, Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));
            user.CreateTime = DateTime.UtcNow;
            user.CreateUserId = userId;

            user.Email = createUser.Email;

            db.TUser.Add(user);


            foreach (var item in roleIds)
            {
                TUserRole userRole = new();
                userRole.Id = idHelper.GetId();
                userRole.CreateTime = DateTime.UtcNow;
                userRole.UserId = user.Id;
                userRole.CreateUserId = this.userId;
                userRole.RoleId = item;

                db.TUserRole.Add(userRole);
            }

            db.SaveChanges();

            return user.Id;
        }




        /// <summary>
        /// 更新用户信息
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="updateUser"></param>
        /// <returns></returns>
        [HttpPost("UpdateUser")]
        public bool UpdateUser(long userId, DtoEditUser updateUser)
        {

            var roleIds = updateUser.RoleIds.Select(t => long.Parse(t)).ToList();

            var user = db.TUser.Where(t => t.IsDelete == false && t.Id == userId).FirstOrDefault();

            if (user != null)
            {
                user.UpdateTime = DateTime.UtcNow;
                user.UpdateUserId = this.userId;

                user.Name = updateUser.Name;
                user.NickName = updateUser.NickName;
                user.Phone = updateUser.Phone;
                user.Email = updateUser.Email;

                if (updateUser.PassWord != "default")
                {
                    user.PassWord = Convert.ToBase64String(KeyDerivation.Pbkdf2(updateUser.PassWord, Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));
                }

                var roleList = db.TUserRole.Where(t => t.IsDelete == false && t.UserId == user.Id).ToList();

                foreach (var item in roleList)
                {
                    if (roleIds.Contains(item.RoleId))
                    {
                        roleIds.Remove(item.RoleId);
                    }
                    else
                    {
                        item.IsDelete = true;
                        item.DeleteTime = DateTime.UtcNow;
                        item.DeleteUserId = this.userId;
                    }
                }

                foreach (var item in roleIds)
                {
                    TUserRole userRole = new();
                    userRole.Id = idHelper.GetId();
                    userRole.CreateTime = DateTime.UtcNow;
                    userRole.UserId = userId;
                    userRole.CreateUserId = this.userId;
                    userRole.RoleId = item;

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



        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("DeleteUser")]
        public bool DeleteUser(long id)
        {
            var user = db.TUser.Where(t => t.IsDelete == false && t.Id == id).FirstOrDefault();

            if (user != null)
            {
                user.IsDelete = true;
                user.DeleteTime = DateTime.UtcNow;
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
        [HttpGet("GetUserFunction")]
        public List<DtoUserFunction> GetUserFunction(long userId)
        {
            var roleIds = db.TUserRole.Where(t => t.IsDelete == false && t.UserId == userId).Select(t => t.RoleId).ToList();

            var functionList = db.TFunction.Where(t => t.IsDelete == false && t.ParentId == null && t.Type == TFunction.EnumType.模块).Select(t => new DtoUserFunction
            {
                Id = t.Id,
                Name = t.Name.Replace(t.Parent!.Name + "-", ""),
                Sign = t.Sign,
                IsCheck = db.TFunctionAuthorize.Where(r => r.IsDelete == false && r.FunctionId == t.Id && (roleIds.Contains(r.RoleId!.Value) || r.UserId == userId)).FirstOrDefault() != null,
                FunctionList = db.TFunction.Where(f => f.IsDelete == false && f.ParentId == t.Id && f.Type == TFunction.EnumType.功能).Select(f => new DtoUserFunction
                {
                    Id = f.Id,
                    Name = f.Name.Replace(f.Parent!.Name + "-", ""),
                    Sign = f.Sign,
                    IsCheck = db.TFunctionAuthorize.Where(r => r.IsDelete == false && r.FunctionId == f.Id && (roleIds.Contains(r.RoleId!.Value) || r.UserId == userId)).FirstOrDefault() != null,
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
        [HttpPost("SetUserFunction")]
        public bool SetUserFunction(DtoSetUserFunction setUserFunction)
        {

            var roleIds = db.TUserRole.Where(t => t.IsDelete == false && t.UserId == setUserFunction.UserId && t.Role.IsDelete == false).Select(t => t.RoleId).ToList();

            var functionAuthorize = db.TFunctionAuthorize.Where(t => t.IsDelete == false && (roleIds.Contains(t.RoleId!.Value) || t.UserId == setUserFunction.UserId) && t.FunctionId == setUserFunction.FunctionId).FirstOrDefault() ?? new TFunctionAuthorize();

            if (setUserFunction.IsCheck)
            {
                if (functionAuthorize.Id == default)
                {
                    functionAuthorize.Id = idHelper.GetId();
                    functionAuthorize.CreateTime = DateTime.UtcNow;
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
                        var userFunctionList = db.TFunctionAuthorize.Where(t => t.IsDelete == false && t.UserId == setUserFunction.UserId).ToList();

                        foreach (var userFunction in userFunctionList)
                        {
                            userFunction.IsDelete = true;
                            userFunction.DeleteTime = DateTime.UtcNow;
                            userFunction.DeleteUserId = userId;
                        }

                        db.SaveChanges();

                        return true;
                    }
                    else
                    {
                        HttpContext.Response.StatusCode = 400;
                        HttpContext.Items.Add("errMsg", "该权限继承自角色，无法单独删除！");

                        return false;
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
        [HttpGet("GetUserRoleList")]
        public List<DtoUserRole> GetUserRoleList(long userId)
        {
            var list = db.TRole.Where(t => t.IsDelete == false).Select(t => new DtoUserRole
            {
                Id = t.Id,
                Name = t.Name,
                Remarks = t.Remarks,
                IsCheck = db.TUserRole.Where(r => r.IsDelete == false && r.RoleId == t.Id && r.UserId == userId).FirstOrDefault() != null
            }).ToList();

            return list;
        }




        /// <summary>
        /// 设置用户角色
        /// </summary>
        /// <param name="setUserRole"></param>
        /// <returns></returns>
        [QueueLimitFilter()]
        [HttpPost("SetUserRole")]
        public bool SetUserRole(DtoSetUserRole setUserRole)
        {
            var userRole = db.TUserRole.Where(t => t.IsDelete == false && t.RoleId == setUserRole.RoleId && t.UserId == setUserRole.UserId).FirstOrDefault();

            if (setUserRole.IsCheck)
            {
                if (userRole == null)
                {
                    userRole = new TUserRole
                    {
                        Id = idHelper.GetId(),
                        CreateTime = DateTime.UtcNow,
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
                    userRole.DeleteTime = DateTime.UtcNow;
                    userRole.DeleteUserId = userId;

                    db.SaveChanges();
                }
            }

            return true;

        }



    }
}