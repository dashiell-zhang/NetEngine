namespace Logger.Database.Query;


/// <summary>
/// 分页数据信息基类
/// </summary>
public class LogQueryResult
{


    /// <summary>
    /// 数据总量
    /// </summary>
    public int Total { get; set; }




    /// <summary>
    /// 具体数据内容
    /// </summary>
    public List<LogQueryItem> List { get; set; } = [];


}
