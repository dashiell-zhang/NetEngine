namespace Application.Model.Basic.File;
/// <summary>
/// 文件信息
/// </summary>
public class DtoFileInfo
{

    /// <summary>
    /// 文件Id
    /// </summary>
    public long Id { get; set; }


    /// <summary>
    /// 文件名称
    /// </summary>
    public string Name { get; set; }


    /// <summary>
    /// 文件大小
    /// </summary>
    public long Length { get; set; }


    /// <summary>
    /// 文件大小
    /// </summary>
    public string LengthText { get; set; }


    /// <summary>
    /// 标记
    /// </summary>
    public string Sign { get; set; }


    /// <summary>
    /// 保存路径
    /// </summary>
    public string Path { get; set; }


    /// <summary>
    /// 文件Url
    /// </summary>
    public string? Url { get; set; }

}
