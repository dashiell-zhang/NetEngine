using Application.Model.Site.Article;
using SourceGenerator.Runtime.Attributes;
using System.Threading.Tasks;

namespace Client.WebAPI.Services;


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



    

}

