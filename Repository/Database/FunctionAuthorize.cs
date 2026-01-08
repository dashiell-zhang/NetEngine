using Repository.Database.Bases;

namespace Repository.Database;

/// <summary>
/// 功能授权配置表
/// </summary>
public class FunctionAuthorize : CUD_User
{


    /// <summary>
    /// 功能ID
    /// </summary>
    public long FunctionId { get; set; }
    public virtual Function Function { get; set; }



    /// <summary>
    /// 角色ID
    /// </summary>
    public long? RoleId { get; set; }
    public virtual Role? Role { get; set; }




    /// <summary>
    /// 用户信息
    /// </summary>
    public long? UserId { get; set; }
    public virtual User? User { get; set; }


}
