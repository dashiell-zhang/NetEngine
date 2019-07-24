using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Models.JwtBearer;
using Swashbuckle.AspNetCore.Swagger;
using WebApi.Actions;
using WebApi.Filters;
using Methods;

namespace WebApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //注册JWT认证机制
            services.Configure<JwtSettings>(Configuration.GetSection("JwtSettings"));
            var jwtSettings = new JwtSettings();
            Configuration.Bind("JwtSettings", jwtSettings);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(o =>
            {
                //主要是jwt  token参数设置
                o.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                    ValidateLifetime = false

                    /***********************************TokenValidationParameters的参数默认值***********************************/
                    // RequireSignedTokens = true,
                    // SaveSigninToken = false,
                    // ValidateActor = false,
                    // 将下面两个参数设置为false，可以不验证Issuer和Audience，但是不建议这样做。
                    // ValidateAudience = true,
                    // ValidateIssuer = true, 
                    // ValidateIssuerSigningKey = false,
                    // 是否要求Token的Claims中必须包含Expires
                    // RequireExpirationTime = true,
                    // 允许的服务器时间偏移量
                    // ClockSkew = TimeSpan.FromSeconds(300),
                    // 是否验证Token有效期，使用当前时间与Token的Claims中的NotBefore和Expires对比
                    // ValidateLifetime = true

                };
            });


            //注册HttpContext
           Methods.Http.HttpContext.Add(services);

            //注册全局过滤器
            services.AddMvc(config => config.Filters.Add(new GlobalFiler()));


            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);


            //注册跨域信息
            services.AddCors(options => options.AddPolicy("cors", policy => policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials().AllowAnyOrigin()));


            services.AddMvc().AddJsonOptions(options =>
            {
                //设置 Json 默认时间格式
                options.SerializerSettings.DateFormatString = "yyyy-MM-dd hh:mm:ss";
            });

            //注册配置文件信息
            Methods.Start.StartConfiguration.Add(Configuration);


            //注册Swagger生成器，定义一个和多个Swagger 文档
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new Info { Title = "WebApi", Version = "v1" });
                var basePath = AppContext.BaseDirectory;
                var xmlPath = Path.Combine(basePath, "WebApi.xml");
                options.IncludeXmlComments(xmlPath, true);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {

            //注册用户认证机制
            app.UseAuthentication();

            //注册全局异常处理机制
            app.UseExceptionHandler(builder => builder.Run(async context => await GlobalError.ErrorEvent(context)));

            if (env.IsDevelopment())
            {
                //默认错误输出页面
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            //注册HttpContext
            Methods.Http.HttpContext.Initialize(app, env);


            //注册跨域信息
            app.UseCors("cors");

            app.UseHttpsRedirection();
            app.UseMvc();


            //注册HostingEnvironment
            Methods.Start.StartHostingEnvironment.Add(env);


            //启用中间件服务生成Swagger作为JSON端点
            app.UseSwagger();

            //启用中间件服务对swagger-ui，指定Swagger JSON端点
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "WebApi V1");
            });
        }
    }
}
