using System.Threading.Tasks;

namespace Client.WebAPI.Services;

public sealed class DemoService : IDemoService
{
    public string Echo(string name) => $"hello, {name}";

    public async Task<int> AddAsync(int a, int b)
    {
        await Task.Delay(10);
        return a + b;
    }
}

