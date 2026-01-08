using Repository.Bases;

namespace Repository.Database;
public class UserBindExternal : CD
{



    /// <summary>
    /// 用户信息
    /// </summary>
    public long UserId { get; set; }
    public virtual User User { get; set; }



    /// <summary>
    /// App名称
    /// </summary>
    public string AppName { get; set; }




    /// <summary>
    /// AppId
    /// </summary>
    public string AppId { get; set; }



    /// <summary>
    /// 用户绑定ID
    /// </summary>
    public string OpenId { get; set; }



}
