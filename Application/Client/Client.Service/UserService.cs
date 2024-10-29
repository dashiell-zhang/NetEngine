using Client.Interface;
using Client.Interface.Models.User;
using Common;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using Shared.Interface;
using Shared.Model;
using System.Text;

namespace Client.Service
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class UserService(DatabaseContext db, IDistributedCache distributedCache, IUserContext userContext) : IUserService
    {

        private long userId => userContext.UserId;


        public DtoUser? GetUser(long? userId)
        {
            userId ??= this.userId;

            var user = db.TUser.Where(t => t.Id == userId).Select(t => new DtoUser
            {
                Name = t.Name,
                UserName = t.UserName,
                Phone = t.Phone,
                Email = t.Email,
                Roles = string.Join(",", db.TUserRole.Where(r => r.UserId == t.Id).Select(r => r.Role.Name).ToList()),
                CreateTime = t.CreateTime
            }).FirstOrDefault();

            return user;
        }



        public bool EditUserPhoneBySms(DtoKeyValue keyValue)
        {
            string phone = keyValue.Key.ToString()!;

            string key = "VerifyPhone_" + phone;

            var code = distributedCache.GetString(key);


            if (string.IsNullOrEmpty(code) == false && code == keyValue.Value!.ToString())
            {

                var checkPhone = db.TUser.Where(t => t.Id != userId && t.Phone == phone).Count();

                var user = db.TUser.Where(t => t.Id == userId).FirstOrDefault();

                if (user != null)
                {
                    if (checkPhone == 0)
                    {
                        user.Phone = phone;

                        db.SaveChanges();

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


    }
}
