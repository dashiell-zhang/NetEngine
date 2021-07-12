using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Models.JwtBearer;
using Savorboard.CAP.InMemoryMessageQueue;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.IO;
using System.Linq;
using System.Text;
using WebApi.Actions;
using WebApi.Filters;
using WebApi.Libraries.Swagger;
using WebApi.Subscribes;

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

            //为各数据库注入连接字符串
            Repository.Database.dbContext.ConnectionString = Configuration.GetConnectionString("dbConnection");
            services.AddDbContextPool<Repository.Database.dbContext>(options => { }, 100);


            services.AddSingleton<DemoSubscribe>();
            services.AddCap(options =>
            {
                //使用 InMemory 传输消息
                options.UseInMemoryMessageQueue();

                //使用 Redis 传输消息
                //options.UseRedis(Configuration.GetConnectionString("dbConnection"));

                //使用 RabbitMQ 传输消息
                //options.UseRabbitMQ(opt =>
                //{
                //    opt.HostName = "amqp-cn-zvp29alz8001.mq-amqp.cn-shanghai-867405-a.aliyuncs.com";
                //    opt.UserName = "MjphbXFwLWNuLXp2cDI5YWx6ODAwMTpMVEFJNXRQd1ZxcmEzZWZ1dXM3Z3N0TFQ=";
                //    opt.Password = "MjQ0RjBERDZCRjkwQ0ZDRTMwNzA3ODc3MDUxOTIzODU3MTREMDgzQToxNjI2MDUzNzM2NTc4";
                //    opt.VirtualHost = "c";
                //});

                //使用 InMemory 存储执行情况
                options.UseInMemoryStorage();

                //使用 ef 搭配 db 存储执行情况
                //options.UseEntityFramework<Repository.Database.dbContext>();

                options.UseDashboard();
                options.DefaultGroupName = "default";   //默认组名称
                options.GroupNamePrefix = null; //全局组名称前缀
                options.TopicNamePrefix = null; //Topic 统一前缀
                options.Version = "v1";
                options.FailedRetryInterval = 60;   //失败时重试间隔
                options.ConsumerThreadCount = 1;    //消费者线程并行处理消息的线程数，当这个值大于1时，将不能保证消息执行的顺序
                options.FailedRetryCount = 10;  //失败时重试的最大次数
                options.FailedThresholdCallback = null; //重试阈值的失败回调
                options.SucceedMessageExpiredAfter = 24 * 3600; //成功消息的过期时间（秒）
            });


            services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = long.MaxValue;
            });

            services.Configure<KestrelServerOptions>(options =>
            {
                options.AllowSynchronousIO = true;
            });

            services.Configure<IISServerOptions>(options =>
            {
                options.AllowSynchronousIO = true;
            });

            services.AddHsts(options =>
            {
                options.MaxAge = TimeSpan.FromDays(365);
            });

            services.AddControllers();



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
            Libraries.Http.HttpContext.Add(services);

            //注册全局过滤器
            services.AddMvc(config => config.Filters.Add(new GlobalFilter()));


            //注册跨域信息
            services.AddCors(option =>
            {
                option.AddPolicy("cors", policy =>
                {
                    policy.SetIsOriginAllowed(origin => true)
                       .AllowAnyHeader()
                       .AllowAnyMethod()
                       .AllowCredentials();
                });
            });


            services.AddControllers().AddJsonOptions(option =>
            {
                option.JsonSerializerOptions.Converters.Add(new Common.Json.DateTimeConverter());
                option.JsonSerializerOptions.Converters.Add(new Common.Json.DateTimeNullConverter());
                option.JsonSerializerOptions.Converters.Add(new Common.Json.LongConverter());
            });



            //注册配置文件信息
            Libraries.Start.StartConfiguration.Add(Configuration);


            services.AddApiVersioning(options =>
            {
                //通过Header向客户端通报支持的版本
                options.ReportApiVersions = true;

                //允许不加版本标记直接调用接口
                options.AssumeDefaultVersionWhenUnspecified = true;

                //接口默认版本
                //options.DefaultApiVersion = new ApiVersion(1, 0);

                //如果未加版本标记默认以当前最高版本进行处理
                options.ApiVersionSelector = new CurrentImplementationApiVersionSelector(options);

                options.ApiVersionReader = new HeaderApiVersionReader("api-version");
                //options.ApiVersionReader = new QueryStringApiVersionReader("api-version");
            });


            services.AddVersionedApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });


            services.AddTransient<IConfigureOptions<SwaggerGenOptions>, SwaggerConfigureOptions>();


            //注册Swagger生成器，定义一个和多个Swagger 文档
            services.AddSwaggerGen(options =>
            {
                options.OperationFilter<SwaggerOperationFilter>();

                options.MapType<long>(() => new OpenApiSchema { Type = "string", Format = "long" });


                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{typeof(Startup).Assembly.GetName().Name}.xml"), true);

                //其他类库的注释文件
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"Models.xml"), true);


                //开启 Swagger JWT 鉴权模块
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
                {
                    Description = "在下框中输入请求头中需要添加Jwt授权Token：Bearer Token",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    BearerFormat = "JWT",
                    Scheme = "Bearer"
                });
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                        new string[] { }
                    }
                });
            });



            //注册统一模型验证
            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = actionContext =>
                {

                    //获取验证失败的模型字段 
                    var errors = actionContext.ModelState.Where(e => e.Value.Errors.Count > 0).Select(e => e.Value.Errors.First().ErrorMessage).ToList();

                    var dataStr = string.Join(" | ", errors);

                    //设置返回内容
                    var result = new
                    {
                        errMsg = dataStr
                    };

                    return new BadRequestObjectResult(result);
                };
            });


            //注册雪花ID算法示例
            services.AddSingleton(new Common.SnowflakeHelper(0, 0));


        }



        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IApiVersionDescriptionProvider provider)
        {

            //开启倒带模式运行多次读取HttpContext.Body中的内容
            app.Use(next => context =>
            {
                context.Request.EnableBuffering();
                return next(context);
            });

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                //注册全局异常处理机制
                app.UseExceptionHandler(builder => builder.Run(async context => await GlobalError.ErrorEvent(context)));
            }

            app.UseHsts();


            //注册HttpContext
            Libraries.Http.HttpContext.Initialize(app, env);


            //注册跨域信息
            app.UseCors("cors");

            //强制重定向到Https
            app.UseHttpsRedirection();


            app.UseRouting();

            //注册用户认证机制,必须放在 UseCors UseRouting 之后
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            //注册HostingEnvironment
            Libraries.Start.StartHostingEnvironment.Add(env);


            //启用中间件服务生成Swagger作为JSON端点
            app.UseSwagger();

            //启用中间件服务对swagger-ui，指定Swagger JSON端点
            app.UseSwaggerUI(options =>
            {
                foreach (var description in provider.ApiVersionDescriptions)
                {
                    options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
                }

                options.RoutePrefix = "swagger";
            });

        }
    }
}
