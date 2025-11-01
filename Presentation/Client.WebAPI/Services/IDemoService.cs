using SourceGenerator.Abstraction.Attributes;
using System.Threading.Tasks;

namespace Client.WebAPI.Services;

[AutoProxy(EnableLogging = true, CaptureArguments = true, MeasureTime = true)] // 代理后缀固定为 _Proxy
public interface IDemoService
{
    string Echo(string name);
    [Cacheable(TtlSeconds = 120)]
    Task<int> AddAsync(int a, int b);
}
