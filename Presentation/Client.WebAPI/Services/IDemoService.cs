using SourceGenerator.Abstraction.Attributes;

namespace Client.WebAPI.Services;

[AutoProxy]
public interface IDemoService
{

    string Echo(string name);


    [Cacheable(TtlSeconds = 120)]
    Task<int> AddAsync(int a, int b);

}

