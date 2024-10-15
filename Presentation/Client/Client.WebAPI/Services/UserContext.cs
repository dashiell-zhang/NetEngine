using Client.Interface;
using Common;
using System.Security.Claims;

namespace Client.WebAPI.Services
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
    {
        public long UserId => long.Parse(httpContextAccessor.HttpContext!.User.FindFirstValue("userId")!);


        public IEnumerable<Claim> Claims => httpContextAccessor.HttpContext!.User.Claims;
    }
}
