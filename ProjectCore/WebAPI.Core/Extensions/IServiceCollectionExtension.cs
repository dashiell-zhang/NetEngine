using Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using WebAPI.Core.Filters;
using WebAPI.Core.Libraries.Swagger;
using WebAPI.Core.Models.AppSetting;

namespace WebAPI.Core.Extensions
{
    public static class IServiceCollectionExtension
    {


        public static void AddCommonServices(this IServiceCollection services, IConfiguration configuration)
        {

            #region 基础 Server 配置

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

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            });

            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
            });

            #endregion


            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();


            services.AddMvc(options => options.Filters.Add(new ExceptionFilter()));


            //注册跨域信息
            services.AddCors(options =>
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

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                var jwtSetting = configuration.GetRequiredSection("JWT").Get<JWTSetting>()!;
                var issuerSigningKey = ECDsa.Create();
                issuerSigningKey.ImportSubjectPublicKeyInfo(Convert.FromBase64String(jwtSetting.PublicKey), out int i);

                options.TokenValidationParameters = new()
                {
                    ValidIssuer = jwtSetting.Issuer,
                    ValidAudience = jwtSetting.Audience,
                    IssuerSigningKey = new ECDsaSecurityKey(issuerSigningKey)
                };
            });

            //services.AddAuthorizationBuilder()
            //    .SetDefaultPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireAssertion(context => IdentityVerification.Authorization(context)).Build());


            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    // .RequireAssertion(context => IdentityVerification.Authorization(context))
                    .Build();
            });
            #endregion


            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();


            services.AddMvc(options => options.Filters.Add(new ExceptionFilter()));


            //注册跨域信息
            services.AddCors(options =>
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
            services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new Common.JsonConverter.DateTimeConverter());
                options.JsonSerializerOptions.Converters.Add(new Common.JsonConverter.DateTimeOffsetConverter());
                options.JsonSerializerOptions.Converters.Add(new Common.JsonConverter.LongConverter());
                options.JsonSerializerOptions.Converters.Add(new Common.JsonConverter.StringConverter());
                options.JsonSerializerOptions.Converters.Add(new Common.JsonConverter.NullableStructConverterFactory());
            });

            #endregion



            #region 注册 Swagger
            services.AddSwaggerGen(options =>
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
            services.Configure<ApiBehaviorOptions>(options =>
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


            services.BatchRegisterServices();


        }





    }
}
