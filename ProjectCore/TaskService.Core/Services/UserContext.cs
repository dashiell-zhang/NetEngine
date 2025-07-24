using Application.Interface.Authorize;
using Common;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace TaskService.Core.Services
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class UserContext : IUserContext
    {
        public bool IsAuthenticated => false;

        public long UserId => throw new NotImplementedException();

        public IEnumerable<Claim> Claims => throw new NotImplementedException();
    }
}
