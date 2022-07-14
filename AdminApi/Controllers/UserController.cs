using AdminApi.Filters;
using AdminApi.Libraries;
using AdminShared.Models;
using AdminShared.Models.User;
using Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Mvc;
using Repository.Database;
using System.Text;

namespace AdminApi.Controllers
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
        private readonly SnowflakeHelper snowflakeHelper;

        private readonly long userId;


        public UserController(DatabaseContext db, SnowflakeHelper snowflakeHelper, IHttpContextAccessor httpContextAccessor)
        {
            this.db = db;
            this.snowflakeHelper = snowflakeHelper;

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
            var data = new DtoPageList<DtoUser>();

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
                Roles = string.Join(",", db.TUserRole.Where(r => r.IsDelete == false && r.UserId == t.Id).Select(r => r.Role.Name).ToList()),
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

            if (userId == null)
            {
                userId = this.userId;
            }

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
            TUser user = new()
            {
                Id = snowflakeHelper.GetId(),
                Name = createUser.Name,
                NickName = createUser.NickName,
                Phone = createUser.Phone
            };
            user.PassWord = Convert.ToBase64String(KeyDerivation.Pbkdf2(createUser.PassWord, Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));
            user.CreateTime = DateTime.UtcNow;
            user.CreateUserId = userId;

            user.Email = createUser.Email;

            db.TUser.Add(user);

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



    }
}