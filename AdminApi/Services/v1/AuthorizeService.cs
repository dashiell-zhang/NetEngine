using AdminApi.Attributes;
using AdminApi.Libraries.Verify;
using Common;
using Common.DistributedLock;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using System;
using System.Security.Claims;

namespace AdminApi.Services.v1
{

    [Service(ServiceLifetime.Scoped)]
    public class AuthorizeService
    {

        private readonly DatabaseContext db;
        private readonly IDistributedLock distLock;
        private readonly SnowflakeHelper snowflakeHelper;

        public AuthorizeService(DatabaseContext db, IDistributedLock distLock, SnowflakeHelper snowflakeHelper)
        {
            this.db = db;
            this.distLock = distLock;
            this.snowflakeHelper = snowflakeHelper;
        }



        /// <summary>
        /// 通过用户id获取 token
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public string GetTokenByUserId(long userId)
        {

            TUserToken userToken = new()
            {
                Id = snowflakeHelper.GetId(),
                UserId = userId,
                CreateTime = DateTime.UtcNow
            };

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
