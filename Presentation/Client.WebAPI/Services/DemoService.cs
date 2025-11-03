using SourceGenerator.Abstraction.Attributes;
using System.Threading.Tasks;

namespace Client.WebAPI.Services;


[AutoProxy]
public class DemoService : IDemoService
{
    public string Echo(string name) => $"hello, {name}";



    [Cacheable(TtlSeconds = 120)]
    public async Task<int> AddAsync(int a, int b)
    {
        await Task.Delay(10);
        return a + b;
    }
}

