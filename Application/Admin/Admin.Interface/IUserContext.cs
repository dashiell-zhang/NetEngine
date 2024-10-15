using System.Security.Claims;

namespace Admin.Interface
{
    public interface IUserContext
    {

        public long UserId { get; }


        public IEnumerable<Claim> Claims { get; }

    }
}
