using Admin.Interface;
using System.Security.Claims;

namespace Admin.WebAPI.Services
{
    public class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
    {
        public long UserId => long.Parse(httpContextAccessor.HttpContext!.User.FindFirstValue("userId")!);
    }
}
