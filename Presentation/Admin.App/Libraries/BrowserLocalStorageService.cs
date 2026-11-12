using Microsoft.JSInterop;

namespace Admin.App.Libraries;

/// <summary>
/// 定义浏览器本地存储访问能力
/// </summary>
public interface IBrowserLocalStorageService
{

    /// <summary>
    /// 获取字符串值
    /// </summary>
    string? GetItemAsString(string key);


    /// <summary>
    /// 设置字符串值
    /// </summary>
    void SetItemAsString(string key, string value);


    /// <summary>
    /// 移除指定键值
    /// </summary>
    void RemoveItem(string key);
}


/// <summary>
/// 基于浏览器 localStorage 的同步存储服务
/// </summary>
public class BrowserLocalStorageService : IBrowserLocalStorageService
{

    private readonly IJSInProcessRuntime _jsInProcessRuntime;


    /// <summary>
    /// 初始化浏览器本地存储服务
    /// </summary>
    public BrowserLocalStorageService(IJSRuntime jsRuntime)
    {

        _jsInProcessRuntime = jsRuntime as IJSInProcessRuntime
            ?? throw new InvalidOperationException("当前宿主不支持同步浏览器存储访问");
    }


    /// <summary>
    /// 获取字符串值
    /// </summary>
    public string? GetItemAsString(string key)
    {

        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            return _jsInProcessRuntime.Invoke<string?>("localStorage.getItem", key);
        }
        catch (JSException exception)
        {
            throw new InvalidOperationException("无法读取浏览器本地存储，请检查浏览器存储权限", exception);
        }

    }


    /// <summary>
    /// 设置字符串值
    /// </summary>
    public void SetItemAsString(string key, string value)
    {

        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            _jsInProcessRuntime.InvokeVoid("localStorage.setItem", key, value);
        }
        catch (JSException exception)
        {
            throw new InvalidOperationException("无法写入浏览器本地存储，请检查存储空间和浏览器存储权限", exception);
        }

    }


    /// <summary>
    /// 移除指定键值
    /// </summary>
    public void RemoveItem(string key)
    {

        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            _jsInProcessRuntime.InvokeVoid("localStorage.removeItem", key);
        }
        catch (JSException exception)
        {
            throw new InvalidOperationException("无法移除浏览器本地存储项，请检查浏览器存储权限", exception);
        }

    }
}
