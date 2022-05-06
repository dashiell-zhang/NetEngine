using Common;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using System;
using System.Security.Claims;
using AdminApi.Libraries.Verify;

namespace AdminApi.Actions.v1
{
    public class AuthorizeAction
    {

        /// <summary>
        /// 通过用户id获取 token
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public static string GetTokenByUserId(long userId)
        {

            using var scope = Program.ServiceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var snowflakeHelper = scope.ServiceProvider.GetRequiredService<SnowflakeHelper>();

            TUserToken userToken = new();
            userToken.Id = snowflakeHelper.GetId();
            userToken.UserId = userId;
            userToken.CreateTime = DateTime.UtcNow;

            db.TUserToken.Add(userToken);
            db.SaveChanges();

            var claim = new Claim[]
            {
                new Claim("tokenId",userToken.Id.ToString()),
                new Claim("userId",userId.ToString())
            };

            return JWTToken.GetToken(claim);
        }


    }
}
