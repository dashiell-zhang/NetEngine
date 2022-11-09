using Common;
using Microsoft.IdentityModel.Tokens;
using Repository.Database;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using WebAPI.Models.AppSetting;

namespace WebAPI.Services
{

    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class AuthorizeService
    {

        private readonly DatabaseContext db;
        private readonly IDHelper idHelper;
        private readonly IConfiguration configuration;


        public AuthorizeService(DatabaseContext db, IDHelper idHelper, IConfiguration configuration)
        {
            this.db = db;
            this.idHelper = idHelper;
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
                Id = idHelper.GetId(),
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

            var jwtSetting = configuration.GetRequiredSection("JWT").Get<JWTSetting>()!;

            var jwtPrivateKey = ECDsa.Create();
            jwtPrivateKey.ImportECPrivateKey(Convert.FromBase64String(jwtSetting.PrivateKey), out _);
            SigningCredentials creds = new(new ECDsaSecurityKey(jwtPrivateKey), SecurityAlgorithms.EcdsaSha256);
            JwtSecurityToken jwtSecurityToken = new(jwtSetting.Issuer, jwtSetting.Audience, claims, DateTime.UtcNow, DateTime.UtcNow + jwtSetting.Expiry, creds);

            return new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);
        }


    }
}
