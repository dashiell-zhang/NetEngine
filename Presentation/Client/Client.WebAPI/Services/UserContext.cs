using Client.Interface;
using System.Security.Claims;

namespace Client.WebAPI.Services
{
    public class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
    {
        public long UserId => long.Parse(httpContextAccessor.HttpContext!.User.FindFirstValue("userId")!);
    }
}
