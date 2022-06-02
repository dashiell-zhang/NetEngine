using Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Repository.Database;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using WebApi.Models.AppSetting;

namespace WebApi.Services.v1
{

    [Service(ServiceLifetime.Scoped)]
    public class AuthorizeService
    {

        private readonly DatabaseContext db;
        private readonly SnowflakeHelper snowflakeHelper;
        private readonly IConfiguration configuration;


        public AuthorizeService(DatabaseContext db, SnowflakeHelper snowflakeHelper, IConfiguration configuration)
        {
            this.db = db;
            this.snowflakeHelper = snowflakeHelper;
            this.configuration = configuration;
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

            var claims = new Claim[]
            {
                new Claim("tokenId",userToken.Id.ToString()),
                new Claim("userId",userId.ToString())
            };

            var jwtSetting = configuration.GetSection("JWTSetting").Get<JWTSetting>();

            var jwtPrivateKey = ECDsa.Create();
            jwtPrivateKey.ImportECPrivateKey(Convert.FromBase64String(jwtSetting.PrivateKey), out _);
            var creds = new SigningCredentials(new ECDsaSecurityKey(jwtPrivateKey), SecurityAlgorithms.EcdsaSha256);
            var jwtSecurityToken = new JwtSecurityToken(jwtSetting.Issuer, jwtSetting.Audience, claims, DateTime.UtcNow, DateTime.UtcNow + jwtSetting.Expiry, creds);

            return new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);
        }


    }
}
