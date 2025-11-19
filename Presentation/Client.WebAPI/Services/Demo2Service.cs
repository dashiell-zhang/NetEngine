using Application.Model.Site.Article;
using SourceGenerator.Runtime.Attributes;
using System.Threading.Tasks;

namespace Client.WebAPI.Services;


[RegisterService(Lifetime = ServiceLifetime.Scoped)]
[AutoProxy]
public class Demo2Service
{
    public virtual string Echo(string name) => $"hello, {name}";


    [Cacheable(TtlSeconds = 120)]
    public virtual async Task<int> AddAsync(int a, int b,DtoArticle dtoArticle)
    {
        await Task.Delay(10);
        return a + b;
    }


    [Cacheable(TtlSeconds = 120)]
    public virtual int Add(ref int a, int b)
    {
        var s = a + b;

        a = 2019;

        return s;
    }

    public virtual async IAsyncEnumerable<int> StreamNumbers(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await Task.Delay(50); // 模拟异步生产
            yield return i;
        }
    }


    public virtual async Task<IAsyncEnumerable<int>> StreamNumbersAsync(int count)
    {
        // 模拟异步准备阶段，例如远程 IO、DB、权限检查等
        await Task.Delay(100);

        async IAsyncEnumerable<int> Iterator()
        {
            for (var i = 0; i < count; i++)
            {
                await Task.Delay(30);
                yield return i;
            }
        }

        return Iterator();
    }




}

