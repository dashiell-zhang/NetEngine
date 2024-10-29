using Common;
using Shared.Interface;
using System.Security.Claims;

namespace WebAPI.Core.Services
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class UserContext : IUserContext
    {

        private readonly IHttpContextAccessor _httpContextAccessor;


        private readonly Lazy<long> _userId;

        private readonly Lazy<bool> _isAuthenticated;

        private readonly Lazy<IEnumerable<Claim>> _claims;


        public UserContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;

            // 使用 Lazy 仅在首次访问时处理赋值

            _isAuthenticated = new Lazy<bool>(() =>
            {
                var isAuthenticated = _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated;

                return isAuthenticated != null && isAuthenticated.Value;
            });

            _userId = new Lazy<long>(() =>
            {

                var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirstValue("userId");

                if (long.TryParse(userIdClaim, out var userId))
                {
                    return userId;
                }
                else
                {
                    throw new Exception("当前上下文中无有效的 UserId");
                }
            });

            _claims = new Lazy<IEnumerable<Claim>>(() =>
                _httpContextAccessor.HttpContext?.User.Claims ?? Enumerable.Empty<Claim>());
        }



        public bool IsAuthenticated => _isAuthenticated.Value;


        public long UserId => _userId.Value;


        public IEnumerable<Claim> Claims => _claims.Value;

    }
}
