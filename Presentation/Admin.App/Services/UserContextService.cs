using Admin.App.Libraries;
using Application.Model.User.User;
using SourceGenerator.Runtime.Attributes;
using System.Net.Http.Json;

namespace Admin.App.Services;
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class UserContextService
{
    private readonly HttpClient _httpClient;

    private Lazy<Task<DtoUser?>> _user;
    private Lazy<Task<List<string>>> _functionList;


    public UserContextService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _user = CreateUserLazy();
        _functionList = CreateFunctionListLazy();
    }


    #region 对外访问函数
    public Task<DtoUser?> GetUser() => _user.Value;

    public Task<List<string>> GetFunctionList() => _functionList.Value;
    #endregion


    #region 构建数据加载的委托
    private Lazy<Task<DtoUser?>> CreateUserLazy()
    {
        return new Lazy<Task<DtoUser?>>(() =>
            _httpClient.GetFromJsonAsync<DtoUser>("User/GetUser", JsonHelper.DeserializeOpts));
    }


    private Lazy<Task<List<string>>> CreateFunctionListLazy()
    {
        return new Lazy<Task<List<string>>>(async () =>
        {
            var retList = await _httpClient.GetFromJsonAsync<Dictionary<string, string>>("Authorize/GetFunctionList", JsonHelper.DeserializeOpts);

            retList ??= [];

            var functionList = retList.Select(t => t.Key).ToList();

            return functionList;
        });
    }
    #endregion


    #region 刷新数据的函数

    public void RefreshUser()
    {
        _user = CreateUserLazy();
    }


    public void RefreshFunctionList()
    {
        _functionList = CreateFunctionListLazy();
    }

    public void RefreshAll()
    {
        RefreshUser();
        RefreshFunctionList();
    }

    #endregion

}
