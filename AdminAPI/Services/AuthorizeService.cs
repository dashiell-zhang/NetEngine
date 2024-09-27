﻿using Common;
using IdentifierGenerator;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Repository.Database;
using System.Security.Claims;
using System.Security.Cryptography;
using WebAPIBasic.Models.AppSetting;

namespace AdminAPI.Services
{

    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class AuthorizeService(DatabaseContext db, IdService idService, IConfiguration configuration)
    {



        /// <summary>
        /// 通过用户id获取 token
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public string GetTokenByUserId(long userId)
        {

            TUserToken userToken = new()
            {
                Id = idService.GetId(),
                UserId = userId
            };

            db.TUserToken.Add(userToken);
            db.SaveChanges();

            var claims = new Claim[]
            {
                new("tokenId",userToken.Id.ToString()),
                new("userId",userId.ToString())
            };

            var jwtSetting = configuration.GetRequiredSection("JWT").Get<JWTSetting>()!;

            var jwtPrivateKey = ECDsa.Create();
            jwtPrivateKey.ImportECPrivateKey(Convert.FromBase64String(jwtSetting.PrivateKey), out _);
            SigningCredentials signingCredentials = new(new ECDsaSecurityKey(jwtPrivateKey), SecurityAlgorithms.EcdsaSha256);

            var nowTime = DateTime.UtcNow;

            SecurityTokenDescriptor tokenDescriptor = new()
            {
                IssuedAt = nowTime,
                Issuer = jwtSetting.Issuer,
                Audience = jwtSetting.Audience,
                NotBefore = nowTime,
                Subject = new ClaimsIdentity(claims),
                Expires = nowTime + jwtSetting.Expiry,
                SigningCredentials = signingCredentials
            };

            JsonWebTokenHandler jwtTokenHandler = new();

            var jwtToken = jwtTokenHandler.CreateToken(tokenDescriptor);

            return jwtToken;
        }


    }
}
