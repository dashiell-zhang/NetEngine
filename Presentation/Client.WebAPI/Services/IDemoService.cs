using SourceGenerator.Runtime.Attributes;

namespace Client.WebAPI.Services;

public interface IDemoService
{

    string Echo(string name);


    Task<int> AddAsync(int a, int b);

}

