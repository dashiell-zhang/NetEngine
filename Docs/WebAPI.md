# WebAPI 公共能力

本文介绍 `Client.WebAPI` 与 `Admin.WebAPI` 共用的启动链路、配置、认证授权、CORS、Swagger 和健康检查。过滤器的具体使用方式见 [WebAPI 过滤器](WebAPIFilters.md)

## 公共启动链路

两个 WebAPI 宿主都通过 `ProjectCore/WebAPI.Core` 中的扩展方法接入公共能力：

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.SetKestrelConfig();
builder.AddCommonServices();

var app = builder.Build();

app.UseCommonMiddleware();
app.MapControllers();
app.MapHealthChecks("/healthz");
```

| 入口 | 主要职责 |
|---|---|
| `SetKestrelConfig()` | 读取 Kestrel 证书配置并设置 HTTPS 默认行为 |
| `AddCommonServices()` | 注册 MVC、CORS、JWT、授权、JSON、Swagger 和健康检查 |
| `UseCommonMiddleware()` | 接入转发头、请求体重复读取、Swagger、异常处理、CORS、HTTPS、认证和授权 |
| `MapHealthChecks("/healthz")` | 暴露当前 WebAPI 宿主的健康检查端点 |

中间件顺序已经集中在 `UseCommonMiddleware()` 中维护，宿主应调用这个入口，不要在不同 WebAPI 项目中复制一套顺序不同的公共中间件

## 配置文件

公共配置位于各宿主的 `appsettings.json` 与 `appsettings.Development.json`

| 配置节 | 用途 |
|---|---|
| `ConnectionStrings:dbConnection` | 主数据库连接字符串 |
| `ConnectionStrings:dbReadConnection` | 只读数据库连接字符串；为空时回退到主库 |
| `ConnectionStrings:redisConnection` | 分布式缓存、Redis 锁和 Redis 客户端连接字符串 |
| `Cors:AllowedOriginList` | 允许跨域访问的来源列表 |
| `JWT` | Token 签发和验证配置 |
| `RSA` | `RSADecryptFilter` 使用的字段解密密钥 |
| `TencentCloudFileStorage` | 腾讯云文件存储配置 |
| `AliCloudFileStorage` | 阿里云文件存储配置 |
| `FileServerUrl` | 文件访问地址 |

开发环境会加载 `appsettings.Development.json` 并覆盖同名配置。新增配置前，应同时确认两个 WebAPI 宿主是否都需要该配置

LLM 模型与应用配置保存在数据库中，不使用宿主配置文件中的 `LLM:Providers` 配置节，详细说明见 [LLM 调用](LLM.md)

## CORS

CORS 白名单读取自：

```json
{
  "Cors": {
    "AllowedOriginList": [
      "*.xxx.com",
      "localhost",
      "localhost:6000",
      "https://admin.xxx.com",
      "https://localhost:5173",
      "*"
    ]
  }
}
```

支持的匹配方式：

| 写法 | 匹配行为 |
|---|---|
| `*` | 允许全部来源 |
| `*.xxx.com` | 匹配该域名的子域名，不限制协议和端口 |
| `xxx.com` | 匹配指定主机，不限制协议和端口 |
| `localhost` | 匹配本地主机，不限制端口 |
| `localhost:6000` | 同时匹配主机和端口 |
| `https://xxx.com` | 同时匹配协议和主机 |
| `https://localhost:6000` | 同时匹配协议、主机和端口 |

匹配时会忽略主机名大小写。`*.xxx.com` 只匹配带子域名的主机，不匹配根域名 `xxx.com`；如果两者都需要，应分别配置

当前策略允许任意请求头、任意 HTTP 方法和凭据，并将预检结果缓存两小时。开发环境可以在 `appsettings.Development.json` 中单独配置 `localhost` 或指定端口

## JWT 认证与权限校验

`JWT` 配置模型包含：

| 字段 | 用途 |
|---|---|
| `Issuer` | Token 签发者 |
| `Audience` | Token 使用方 |
| `PrivateKey` | 签发 Token 使用的 ECDSA 私钥 |
| `PublicKey` | 验证 Token 使用的 ECDSA 公钥 |
| `Expiry` | Token 有效期 |

公共服务使用 Bearer JWT 作为默认认证方案，并通过 `PublicKey`、`Issuer` 和 `Audience` 验证 Token

Controller 或 Action 使用 `[Authorize]` 后，除了要求用户已经通过认证，还会调用当前宿主的 `IPermissionService` 校验接口权限。明确允许匿名访问的接口使用 `[AllowAnonymous]`

管理端和客户端 API 分别提供自己的 `PermissionService` 实现。权限校验期间如果生成了新 Token，会通过响应头 `NewToken` 返回，并通过 `Access-Control-Expose-Headers` 允许浏览器读取

Swagger 会为需要授权且没有 `[AllowAnonymous]` 的接口添加 Bearer Token 输入支持

## RSA 字段解密

使用 `RSADecryptFilter` 时，宿主需要提供：

```json
{
  "RSA": {
    "PublicKey": "",
    "PrivateKey": ""
  }
}
```

过滤器通过 `PrivateKey` 解密标记了 `[RSAEncrypted]` 的请求字段。具体标记方式、支持的参数形态和执行行为见 [WebAPI 过滤器](WebAPIFilters.md#rsadecryptfilter)

## Swagger 与异常处理

- Development 环境启用 Swagger 和 Swagger UI，默认入口为 `/swagger`
- 非 Development 环境启用响应压缩和统一异常处理中间件
- MVC 始终注册全局 `ExceptionFilter`，用于处理进入 MVC 过滤器管线后的异常
- 请求模型验证失败时，返回包含 `errMsg` 的 `400 Bad Request`

WebAPI 启动后，Debug 构建会在控制台输出 Swagger 地址

## 健康检查

`Client.WebAPI` 与 `Admin.WebAPI` 都将健康检查映射到：

```text
/healthz
```

当前检查项包括：

- `CacheHealthCheck`：通过 `IDistributedCache` 验证缓存写入
- `DatabaseHealthCheck`：通过 `DatabaseContext` 验证数据库连接

健康检查后台发布器启动后延迟 10 秒执行，之后每 60 秒运行一次。`TaskService` 是 Worker Service 宿主，不提供 `/healthz` HTTP 路由

## 新增或调整 WebAPI 宿主检查清单

- 调用 `SetKestrelConfig()`、`AddCommonServices()` 与 `UseCommonMiddleware()`
- 注册数据库、缓存和 `IPermissionService` 等公共能力需要的依赖
- 调用 `BatchRegisterServices()` 与 `BatchRegisterBackgroundServices()` 保持生成式注册链路
- 映射 Controller 和 `/healthz`
- 同时检查 `appsettings.json` 与 `appsettings.Development.json`
- 为需要权限的 Controller 或 Action 添加 `[Authorize]`
- 只有明确公开的接口才添加 `[AllowAnonymous]`
- 使用公共过滤器前阅读 [WebAPI 过滤器](WebAPIFilters.md) 并确认依赖已经注册
