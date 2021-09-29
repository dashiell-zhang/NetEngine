using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Dtos;
using System;
using System.Linq;
using AdminApi.Filters;
using AdminApi.Libraries;
using AdminApi.Libraries.Verify;
using AdminApi.Models.v1.User;
using Repository.Database;

namespace AdminApi.Controllers.v1
{


    /// <summary>
    /// 用户数据操作控制器
    /// </summary>
    [ApiVersion("1")]
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class UserController : ControllerCore
    {


        /// <summary>
        /// 获取用户列表
        /// </summary>
        /// <param name="pageNum"></param>
        /// <param name="pageSize"></param>
        /// <param name="searchKey"></param>
        /// <returns></returns>
        [HttpGet("GetUserList")]
        public dtoPageList<dtoUser> GetUserList(int pageNum, int pageSize, string searchKey)
        {
            var data = new dtoPageList<dtoUser>();

            int skip = (pageNum - 1) * pageSize;

            var query = db.TUser.Where(t => t.IsDelete == false);

            if (!string.IsNullOrEmpty(searchKey))
            {
                query = query.Where(t => t.Name.Contains(searchKey) | t.NickName.Contains(searchKey) | t.Phone.Contains(searchKey));
            }


            data.Total = query.Count();

            data.List = query.OrderByDescending(t => t.CreateTime).Select(t => new dtoUser
            {
                Id = t.Id,
                Name = t.Name,
                NickName = t.NickName,
                Phone = t.Phone,
                Email = t.Email,
                Roles = string.Join(",", db.TUserRole.Where(r => r.IsDelete == false & r.UserId == t.Id).Select(r => r.Role.Name).ToList()),
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
        public dtoUser GetUser(Guid? userId)
        {

            if (userId == null)
            {
                userId = base.userId;
            }

            var user = db.TUser.Where(t => t.Id == userId && t.IsDelete == false).Select(t => new dtoUser
            {
                Id = t.Id,
                Name = t.Name,
                NickName = t.NickName,
                Phone = t.Phone,
                Email = t.Email,
                Roles = string.Join(",", db.TUserRole.Where(r => r.IsDelete == false & r.UserId == t.Id).Select(r => r.Role.Name).ToList()),
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
        public Guid CreateUser(dtoEditUser createUser)
        {
            var user = new TUser();
            user.Id = Guid.NewGuid();
            user.CreateTime = DateTime.Now;
            user.CreateUserId = userId;

            user.Name = createUser.Name;
            user.NickName = createUser.NickName;
            user.Phone = createUser.Phone;
            user.Email = createUser.Email;
            user.PassWord = createUser.PassWord;

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
        public bool UpdateUser(Guid userId, dtoEditUser updateUser)
        {
            var user = db.TUser.Where(t => t.IsDelete == false & t.Id == userId).FirstOrDefault();

            user.UpdateTime = DateTime.Now;
            user.UpdateUserId = base.userId;

            user.Name = updateUser.Name;
            user.NickName = updateUser.NickName;
            user.Phone = updateUser.Phone;
            user.Email = updateUser.Email;

            if (!string.IsNullOrEmpty(updateUser.PassWord))
            {
                user.PassWord = updateUser.PassWord;
            }

            db.SaveChanges();

            return true;
        }



        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("DeleteUser")]
        public bool DeleteUser(dtoId id)
        {
            var user = db.TUser.Where(t => t.IsDelete == false & t.Id == id.Id).FirstOrDefault();

            user.IsDelete = true;
            user.DeleteTime = DateTime.Now;
            user.DeleteUserId = userId;

            db.SaveChanges();

            return true;
        }



    }
}