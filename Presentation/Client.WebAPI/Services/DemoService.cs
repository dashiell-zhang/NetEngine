using SourceGenerator.Runtime.Attributes;

namespace Client.WebAPI.Services;


[AutoProxy]
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class DemoService(IServiceProvider serviceProvider) : IDemoService
{
    public string Echo(string name) => $"hello, {name}";



    [Cacheable(TtlSeconds = 120)]
    public async Task<int> AddAsync(int a, int b)
    {
        serviceProvider.CreateScope();
        await Task.Delay(10);
        return a + b;
    }
}

