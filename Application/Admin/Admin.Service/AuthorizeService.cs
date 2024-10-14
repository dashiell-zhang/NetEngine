using Admin.Interface;
using Admin.Model.Authorize;
using Common;
using IdentifierGenerator;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Repository.Database;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using WebAPI.Core.Libraries;
using WebAPI.Core.Models.AppSetting;

namespace Admin.Service
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class AuthorizeService(IHttpContextAccessor httpContextAccessor, DatabaseContext db, IdService idService, IConfiguration configuration) : IAuthorizeService
    {

        private long userId => httpContextAccessor.HttpContext!.User.GetClaim<long>("userId");


        public string? GetToken(DtoLogin login)
        {
            var userList = db.TUser.Where(t => t.UserName == login.UserName).Select(t => new { t.Id, t.Password }).ToList();

            var user = userList.Where(t => t.Password == Convert.ToBase64String(KeyDerivation.Pbkdf2(login.Password, Encoding.UTF8.GetBytes(t.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32))).FirstOrDefault();

            if (user != null)
            {
                return GetTokenByUserId(user.Id);
            }
            else
            {
                throw new CustomException("用户名或密码错误");
            }

        }




        public List<string> GetFunctionList()
        {
            var roleIds = db.TUserRole.AsNoTracking().Where(t => t.UserId == userId).Select(t => t.RoleId).ToList();

            var kvList = db.TFunctionAuthorize.Where(t => (roleIds.Contains(t.RoleId!.Value) || t.UserId == userId)).Select(t =>
                t.Function.Sign
            ).ToList();

            return kvList;
        }



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
