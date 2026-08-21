using WebAPI.Core.Libraries;

namespace WebAPI.Core.Extensions;

/// <summary>
/// Web 应用程序公共扩展
/// </summary>
public static class WebApplicationExtension
{


    /// <summary>
    /// 注册 WebAPI 公共中间件
    /// </summary>
    public static void UseCommonMiddleware(this WebApplication app)
    {
        app.UseForwardedHeaders();

        //开启倒带模式允许多次读取 HttpContext.Body 中的内容
        app.Use(async (context, next) =>
        {
            context.Request.EnableBuffering();
            await next.Invoke();
        });

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();

            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint($"/swagger/v1/swagger.json", null);
            });
        }
        else
        {
            app.UseResponseCompression();

            //注册全局异常处理机制
            app.UseExceptionHandler(builder => builder.Run(async context => await GlobalError.ErrorEvent(context)));
        }

        app.UseHsts();


        app.UseHttpsRedirection();

        app.UseRouting();

        //注册跨域信息
        app.UseCors("cors");

        //注册用户认证机制 必须放在 UseRouting 和 UseCors 之后
        app.UseAuthentication();
        app.UseAuthorization();

    }




    public static void ShowDocUrl(this WebApplication app)
    {
#if DEBUG
        string url = app.Urls.First().Replace("http://[::]", "http://127.0.0.1");
        Console.WriteLine(Environment.NewLine + "Swagger Doc: " + url + "/swagger/" + Environment.NewLine);
#endif
    }




    /// <summary>
    /// 初始化所有不包含开放泛型的单例服务
    /// </summary>
    /// <param name="app"></param>
    /// <param name="builderServices"></param>
    public static void InitSingletonService(this WebApplication app, IServiceCollection builderServices)
    {
        var serviceTypeList = builderServices.Where(t => t.Lifetime == ServiceLifetime.Singleton && t.ServiceType.ContainsGenericParameters == false).Select(t => t.ServiceType).ToList();

        foreach (var serviceType in serviceTypeList)
        {
            app.Services.GetService(serviceType);
        }
    }



}
