using WebAPI.Core.Libraries;

namespace WebAPI.Core.Extensions
{
    public static class IApplicationBuilderExtension
    {


        public static void UseCommonMiddleware(this IApplicationBuilder app, IHostEnvironment env)
        {
            app.UseForwardedHeaders();

            //开启倒带模式允许多次读取 HttpContext.Body 中的内容
            app.Use(async (context, next) =>
            {
                context.Request.EnableBuffering();
                await next.Invoke();
            });

            if (env.IsDevelopment())
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


            //注册跨域信息
            app.UseCors("cors");

            app.UseHttpsRedirection();

            app.UseRouting();

            //注册用户认证机制,必须放在 UseCors UseRouting 之后
            app.UseAuthentication();
            app.UseAuthorization();

        }

    }
}
