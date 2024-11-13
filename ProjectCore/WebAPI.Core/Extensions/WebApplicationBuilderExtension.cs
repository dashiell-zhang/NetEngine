using Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Repository.HealthCheck;
using Shared.Model.AppSetting;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using WebAPI.Core.Filters;
using WebAPI.Core.Interfaces;
using WebAPI.Core.Libraries.HealthCheck;
using WebAPI.Core.Libraries.Swagger;

namespace WebAPI.Core.Extensions
{
    public static class WebApplicationBuilderExtension
    {

        /// <summary>
        /// 设置 Kestrel 配置
        /// </summary>
        /// <param name="builder"></param>
        public static void SetKestrelConfig(this WebApplicationBuilder builder)
        {
            builder.WebHost.UseKestrel((context, options) =>
            {
                options.ConfigureHttpsDefaults(options =>
                {
                    var certConf = context.Configuration.GetSection("Kestrel:Certificates:Default");

                    if (certConf.Value != null)
                    {
                        X509Certificate2Collection x509Certificate2s = new();

                        var sslPath = certConf.GetValue<string>("Path")!;

                        if (sslPath.EndsWith("pfx", StringComparison.OrdinalIgnoreCase))
                        {
                            string password = certConf.GetValue<string>("Password")!;

                            x509Certificate2s = X509CertificateLoader.LoadPkcs12CollectionFromFile(sslPath, password);
                        }
                        else if (sslPath.EndsWith("pem", StringComparison.OrdinalIgnoreCase) || sslPath.EndsWith("crt", StringComparison.OrdinalIgnoreCase))
                        {
                            x509Certificate2s.ImportFromPemFile(sslPath);
                        }
                        options.ServerCertificateChain = x509Certificate2s;
                    }
                });
            });
        }



        public static void AddCommonServices(this WebApplicationBuilder builder)
        {

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


            builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();


            builder.Services.AddMvc(options => options.Filters.Add(new ExceptionFilter()));


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

            #region 注册 JWT 认证机制

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
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
                options.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                    {
                        if (context.Resource is HttpContext httpContext)
                        {
                            var permissionService = httpContext.RequestServices.GetService<IPermissionService>();

                            return permissionService != null && permissionService.VerifyAuthorization(context);
                        }
                        else
                        {
                            return false;
                        }
                    })
                    .Build();
            });
            #endregion


            builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();


            builder.Services.AddMvc(options => options.Filters.Add(new ExceptionFilter()));


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
                //options.JsonSerializerOptions.Converters.Add(new Common.JsonConverter.DateTimeConverter());
                //options.JsonSerializerOptions.Converters.Add(new Common.JsonConverter.DateTimeOffsetConverter());
                options.JsonSerializerOptions.Converters.Add(new Common.JsonConverter.LongConverter());
                options.JsonSerializerOptions.Converters.Add(new Common.JsonConverter.StringConverter());
                options.JsonSerializerOptions.Converters.Add(new Common.JsonConverter.NullableStructConverterFactory());
            });

            #endregion



            #region 注册 Swagger
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", null);

                var modelPrefix = Assembly.GetEntryAssembly()?.GetName().Name + ".Models.";
                options.SchemaGeneratorOptions = new() { SchemaIdSelector = type => type.ToString()[(type.ToString().IndexOf("Models.") + 7)..].Replace(modelPrefix, "").Replace("`1", "").Replace("+", ".") };

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


            builder.Services.BatchRegisterServices();



            #region 注册健康检测服务
            builder.Services.AddHealthChecks()
                .AddCheck<CacheHealthCheck>("CacheHealthCheck")
                .AddCheck<DatabaseHealthCheck>("DatabaseHealthCheck");


            builder.Services.Configure<HealthCheckPublisherOptions>(options =>
            {
                options.Delay = TimeSpan.FromSeconds(10);
                options.Period = TimeSpan.FromSeconds(60);
            });

            builder.Services.AddSingleton<IHealthCheckPublisher, HealthCheckPublisher>();
            #endregion

        }


    }
}
