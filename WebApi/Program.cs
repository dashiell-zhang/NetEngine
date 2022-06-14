using Common;
using DistributedLock;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Swagger;
using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using WebApi.Filters;
using WebApi.Libraries;
using WebApi.Libraries.HttpHandler;
using WebApi.Models.AppSetting;

namespace WebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {

            EnvironmentHelper.ChangeDirectory(args);

            var builder = WebApplication.CreateBuilder(args);

            #region 启用 Kestrel Https 并绑定证书

            //builder.WebHost.UseKestrel(options =>
            //{
            //    options.ConfigureHttpsDefaults(options =>
            //    {
            //        options.ServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(Path.Combine(AppContext.BaseDirectory, "xxxx.pfx"), "123456");
            //    });
            //});
            //builder.WebHost.UseUrls("https://*");

            #endregion


            builder.Services.AddDbContextPool<Repository.Database.DatabaseContext>(options =>
            {
                options.UseSqlServer(builder.Configuration.GetConnectionString("dbConnection"));
            }, 100);


            #region 基础 Server 配置

            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = long.MaxValue;
            });

            builder.Services.Configure<KestrelServerOptions>(options =>
            {
                options.AllowSynchronousIO = true;
            });

            builder.Services.Configure<IISServerOptions>(options =>
            {
                options.AllowSynchronousIO = true;
            });

            builder.Services.AddHsts(options =>
            {
                options.MaxAge = TimeSpan.FromDays(365);
            });

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            });

            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
            });

            #endregion

            builder.Services.AddControllers();


            #region 注册 JWT 认证机制

            var jwtSetting = builder.Configuration.GetSection("JWT").Get<JWTSetting>();
            var issuerSigningKey = ECDsa.Create();
            issuerSigningKey.ImportSubjectPublicKeyInfo(Convert.FromBase64String(jwtSetting.PublicKey), out int i);
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwtSetting.Issuer,
                    ValidAudience = jwtSetting.Audience,
                    IssuerSigningKey = new ECDsaSecurityKey(issuerSigningKey)
                };
            });

            builder.Services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireAssertion(context => IdentityVerification.Authorization(context)).Build();
            });

            #endregion

            //注册HttpContext
            builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();


            //注册全局过滤器
            builder.Services.AddMvc(options => options.Filters.Add(new GlobalFilter()));


            //注册跨域信息
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("cors", policy =>
                {
                    policy.SetIsOriginAllowed(origin => true)
                       .AllowAnyHeader()
                       .AllowAnyMethod()
                       .AllowCredentials();
                });
            });


            #region 注册 Json 序列化配置
            builder.Services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new Common.JsonConverter.DateTimeConverter());
                options.JsonSerializerOptions.Converters.Add(new Common.JsonConverter.DateTimeNullConverter());
                options.JsonSerializerOptions.Converters.Add(new Common.JsonConverter.DateTimeOffsetConverter());
                options.JsonSerializerOptions.Converters.Add(new Common.JsonConverter.DateTimeOffsetNullConverter());
                options.JsonSerializerOptions.Converters.Add(new Common.JsonConverter.LongConverter());
            });

            #endregion

            #region 注册 api 版本控制

            builder.Services.AddApiVersioning(options =>
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
            });

            builder.Services.AddVersionedApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

            #endregion

            builder.Services.AddMySwagger(true);

            //注册统一模型验证
            builder.Services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = actionContext =>
                {

                    //获取验证失败的模型字段 
                    var errors = actionContext.ModelState.Where(e => e.Value?.Errors.Count > 0).Select(e => e.Value?.Errors.First().ErrorMessage).ToList();

                    var dataStr = string.Join(" | ", errors);

                    //设置返回内容
                    var result = new
                    {
                        errMsg = dataStr
                    };

                    return new BadRequestObjectResult(result);
                };
            });


            //注册雪花ID算法
            builder.Services.AddSingleton(new SnowflakeHelper(0, 0));

            #region 注册分布式锁

            //注册分布式锁 Redis模式
            //builder.Services.AddSingleton<IDistributedLock>(new RedisLock(builder.Configuration.GetConnectionString("redisConnection")));

            //注册分布式锁 数据库模式
            builder.Services.AddScoped<IDistributedLock, DataBaseLock>();

            #endregion

            #region 注册缓存服务

            //注册缓存服务 内存模式
            builder.Services.AddDistributedMemoryCache();

            //注册缓存服务 SqlServer模式
            //builder.Services.AddDistributedSqlServerCache(options =>
            //{
            //    options.ConnectionString = builder.Configuration.GetConnectionString("dbConnection");
            //    options.SchemaName = "dbo";
            //    options.TableName = "t_cache";
            //});

            //注册缓存服务 Redis模式
            //builder.Services.AddStackExchangeRedisCache(options =>
            //{
            //    options.Configuration = builder.Configuration.GetConnectionString("redisConnection");
            //    options.InstanceName = "cache";
            //});

            #endregion

            #region 注册HttpClient
            builder.Services.AddHttpClient("", options =>
            {
                options.DefaultRequestVersion = new Version("2.0");
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            });


            builder.Services.AddHttpClient("SkipSsl", options =>
            {
                options.DefaultRequestVersion = new Version("2.0");
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; }
            });


            builder.Services.AddScoped<HttpSignHandler>();
            builder.Services.AddHttpClient("HttpSign", options =>
            {
                options.DefaultRequestVersion = new Version("2.0");
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
            }).AddHttpMessageHandler(t => t.GetRequiredService<HttpSignHandler>()); ;

            #endregion

            builder.Services.BatchRegisterServices();

            #region 注册短信服务

            //注册腾讯云短信服务
            //var tencentCloudSMSSetting = builder.Configuration.GetSection("TencentCloudSMS").Get<TencentCloudSMSSetting>();
            //builder.Services.AddSingleton<ISMS>(new TencentCloudSMS(tencentCloudSMSSetting.AppId, tencentCloudSMSSetting.SecretId, tencentCloudSMSSetting.SecretKey));


            //注册阿里云短信服务
            //var aliCloudSMSSetting = builder.Configuration.GetSection("AliCloudSMS").Get<AliCloudSMSSetting>();
            //builder.Services.AddSingleton<ISMS>(new AliCloudSMS(aliCloudSMSSetting.AccessKeyId, aliCloudSMSSetting.AccessKeySecret));

            #endregion

            #region 注册文件服务

            //注册腾讯云COS文件服务
            //var tencentCloudFileStorageSetting = builder.Configuration.GetSection("TencentCloudFileStorage").Get<TencentCloudFileStorageSetting>();
            //builder.Services.AddSingleton<IFileStorage>(new TencentCloudFileStorage(tencentCloudFileStorageSetting.AppId, tencentCloudFileStorageSetting.Region, tencentCloudFileStorageSetting.SecretId, tencentCloudFileStorageSetting.SecretKey, tencentCloudFileStorageSetting.BucketName));


            //注册阿里云OSS文件服务
            //var aliCloudFileStorageSetting = builder.Configuration.GetSection("AliCloudFileStorage").Get<AliCloudFileStorageSetting>();
            //builder.Services.AddSingleton<IFileStorage>(new AliCloudFileStorage(aliCloudFileStorageSetting.Endpoint, aliCloudFileStorageSetting.AccessKeyId, aliCloudFileStorageSetting.AccessKeySecret, aliCloudFileStorageSetting.BucketName));

            #endregion

            var app = builder.Build();

            app.UseForwardedHeaders();

            app.UseResponseCompression();

            //开启倒带模式允许多次读取 HttpContext.Body 中的内容
            app.Use(async (context, next) =>
            {
                context.Request.EnableBuffering();
                await next.Invoke();
            });

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
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

            app.MapControllers();


            app.UseMySwagger();


            app.Run();
        }


    }
}
