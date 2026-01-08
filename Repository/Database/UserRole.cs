using Repository.Database.Bases;

namespace Repository.Database;
public class UserRole : CUD_User
{


    /// <summary>
    /// 角色信息
    /// </summary>
    public long RoleId { get; set; }
    public virtual Role Role { get; set; }



    /// <summary>
    /// 用户信息
    /// </summary>
    public long UserId { get; set; }
    public virtual User User { get; set; }


}
