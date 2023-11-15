using Common;
using DistributedLock.Redis;
using Logger.DataBase;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using WebAPI.Filters;
using WebAPI.Libraries;
using WebAPI.Libraries.HealthCheck;
using WebAPI.Libraries.HttpHandler;
using WebAPI.Libraries.Swagger;
using WebAPI.Models.AppSetting;

namespace WebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {

            ThreadPool.SetMinThreads(128, 1);

            EnvironmentHelper.ChangeDirectory(args);

            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseWindowsService();

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
                options.UseNpgsql(builder.Configuration.GetConnectionString("dbConnection")!);
            }, 30);


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

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                var jwtSetting = builder.Configuration.GetRequiredSection("JWT").Get<JWTSetting>()!;
                var issuerSigningKey = ECDsa.Create();
                issuerSigningKey.ImportSubjectPublicKeyInfo(Convert.FromBase64String(jwtSetting.PublicKey), out int i);

                options.TokenValidationParameters = new()
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
                       .AllowCredentials()
                       .SetPreflightMaxAge(TimeSpan.FromSeconds(7200));
                });
            });


            #region 注册 Json 序列化配置
            builder.Services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new Common.JsonConverter.DateTimeConverter());
                options.JsonSerializerOptions.Converters.Add(new Common.JsonConverter.DateTimeOffsetConverter());
                options.JsonSerializerOptions.Converters.Add(new Common.JsonConverter.LongConverter());
                options.JsonSerializerOptions.Converters.Add(new Common.JsonConverter.NullableStructConverterFactory());
            });

            #endregion


            #region 注册 Swagger
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", null);

                var modelPrefix = Assembly.GetEntryAssembly()?.GetName().Name + ".Models.";
                options.SchemaGeneratorOptions = new() { SchemaIdSelector = type => (type.ToString()[(type.ToString().IndexOf("Models.") + 7)..]).Replace(modelPrefix, "").Replace("`1", "").Replace("+", ".") };

                options.MapType<long>(() => new OpenApiSchema { Type = "string", Format = "long" });

                var xmlPaths = IOHelper.GetFolderAllFiles(AppContext.BaseDirectory).Where(t => t.EndsWith(".xml")).ToList();
                foreach (var xmlPath in xmlPaths)
                {
                    options.IncludeXmlComments(xmlPath, true);
                }

                options.AddSecurityDefinition("bearerAuth", new OpenApiSecurityScheme()
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                });
                options.OperationFilter<SecurityRequirementsOperationFilter>();
            });
            #endregion


            //注册统一模型验证
            builder.Services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = actionContext =>
                {
                    //获取验证失败的模型字段 
                    var errors = actionContext.ModelState.Where(e => e.Value?.Errors.Count > 0).Select(e => e.Key + " : " + e.Value?.Errors.First().ErrorMessage).ToList();

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
            builder.Services.AddSingleton(new IDHelper(0, 0));


            //注册分布式锁 Redis模式
            builder.Services.AddRedisLock(options =>
            {
                options.Configuration = builder.Configuration.GetConnectionString("redisConnection")!;
                options.InstanceName = "lock";
            });


            //注册缓存服务 Redis模式
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = builder.Configuration.GetConnectionString("redisConnection");
                options.InstanceName = "cache";
            });


            //注册 Redis 驱动
            builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("redisConnection")!));


            #region 注册HttpClient

            builder.Services.AddHttpClient("", options =>
            {
                options.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = System.Net.DecompressionMethods.All
            });

            builder.Services.AddHttpClient("SkipSsl", options =>
            {
                options.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; }
            });

            builder.Services.AddTransient<HttpSignHandler>();
            builder.Services.AddHttpClient("HttpSign", options =>
            {
                options.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = System.Net.DecompressionMethods.All,
            }).AddHttpMessageHandler<HttpSignHandler>();


            builder.Services.AddHttpClient("CarryCert", options =>
            {
                options.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
            }).ConfigurePrimaryHttpMessageHandler(() =>
            {
                using HttpClientHandler handler = new()
                {
                    AllowAutoRedirect = false,
                    AutomaticDecompression = System.Net.DecompressionMethods.All
                };
                var sslPath = Path.Combine(Directory.GetCurrentDirectory(), "ssl", "xxxx.p12");
                using X509Certificate2 certificate = new(sslPath, "证书密码", X509KeyStorageFlags.MachineKeySet);
                handler.ClientCertificates.Add(certificate);
                return handler;
            });

            #endregion

            builder.Services.BatchRegisterServices();

            #region 注册短信服务

            //builder.Services.AddTencentCloudSMS(options =>
            //{
            //    var settings = builder.Configuration.GetRequiredSection("TencentCloudSMS").Get<SMS.TencentCloud.Models.SMSSetting>()!;
            //    options.AppId = settings.AppId;
            //    options.SecretId = settings.SecretId;
            //    options.SecretKey = settings.SecretKey;
            //});


            //builder.Services.AddAliCloudSMS(options =>
            //{
            //    var settings = builder.Configuration.GetRequiredSection("AliCloudSMS").Get<SMS.AliCloud.Models.SMSSetting>()!;
            //    options.AccessKeyId = settings.AccessKeyId;
            //    options.AccessKeySecret = settings.AccessKeySecret;
            //});

            #endregion

            #region 注册文件服务


            //builder.Services.AddTencentCloudStorage(options =>
            //{
            //    var settings = builder.Configuration.GetRequiredSection("TencentCloudFileStorage").Get<FileStorage.TencentCloud.Models.FileStorageSetting>()!;
            //    options.AppId = settings.AppId;
            //    options.Region = settings.Region;
            //    options.SecretId = settings.SecretId;
            //    options.SecretKey = settings.SecretKey;
            //    options.BucketName = settings.BucketName;
            //});


            //builder.Services.AddAliCloudStorage(options =>
            //{
            //    var settings = builder.Configuration.GetRequiredSection("AliCloudFileStorage").Get<FileStorage.AliCloud.Models.FileStorageSetting>()!;
            //    options.Endpoint = settings.Endpoint;
            //    options.AccessKeyId = settings.AccessKeyId;
            //    options.AccessKeySecret = settings.AccessKeySecret;
            //    options.BucketName = settings.BucketName;
            //});

            #endregion

            #region 注册日志服务

            //注册数据库日志服务
            builder.Logging.AddDataBaseLogger(options => { });

            //注册本地文件日志服务
            //builder.Logging.AddLocalFileLogger(options => { });

            #endregion

            #region 注册健康检测服务
            builder.Services.AddHealthChecks()
                .AddCheck<CacheHealthCheck>("CacheHealthCheck")
                .AddDbContextCheck<Repository.Database.DatabaseContext>("DatabaseHealthCheck");


            builder.Services.Configure<HealthCheckPublisherOptions>(options =>
            {
                options.Delay = TimeSpan.FromSeconds(10);
                options.Period = TimeSpan.FromSeconds(60);
            });

            builder.Services.AddSingleton<IHealthCheckPublisher, HealthCheckPublisher>();
            #endregion

            var app = builder.Build();

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


            //注册跨域信息
            app.UseCors("cors");

            app.UseHttpsRedirection();

            app.UseRouting();

            //注册用户认证机制,必须放在 UseCors UseRouting 之后
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();


            app.MapHealthChecks("/healthz");

            app.Start();
#if DEBUG
            string url = app.Urls.First().Replace("http://[::]", "http://127.0.0.1");
            Console.WriteLine(Environment.NewLine + "Swagger Doc: " + url + "/swagger/" + Environment.NewLine);
#endif
            app.WaitForShutdown();
        }


    }
}
