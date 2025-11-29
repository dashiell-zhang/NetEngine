namespace FileStorage.AliCloud.Models;
public class FileStorageSetting
{

    /// <summary>
    /// OSS存储区域
    /// </summary>
    public string Region { get; set; }


    /// <summary>
    /// 使用内部节点
    /// </summary>
    public bool UseInternalEndpoint { get; set; }


    /// <summary>
    /// 账户ID
    /// </summary>
    public string AccessKeyId { get; set; }


    /// <summary>
    /// 账户私钥
    /// </summary>
    public string AccessKeySecret { get; set; }


    /// <summary>
    /// 存储桶名称
    /// </summary>
    public string BucketName { get; set; }


    /// <summary>
    /// 访问Url
    /// </summary>
    public string Url { get; set; }
}



