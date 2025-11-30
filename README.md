# NetEngine 项目框架

基于最新 .NET 平台（.NET 10）搭建的通用项目框架，包含 Web API、Blazor 管理后台、定时任务服务、分布式锁、文件存储、短信、支付等常用基础能力，帮助你在启动新项目时快速进入业务开发阶段。

> 设计理念：保持接近微软官方示例的代码风格，尽量少封装、易裁剪、易读、易维护。

---

## 1. 功能特性总览

- 认证与安全
  - 基于 JWT 的认证授权，支持 ECDSA 公私钥签名
  - 内置请求签名校验（防篡改、防重放）、RSA 字段解密
- 缓存与分布式能力
  - Redis 分布式缓存 + HybridCache 本地缓存
  - Redis 分布式锁（互斥锁 & 信号量）
- 支付与登录能力
  - 支付宝（PC / H5）支付
  - 微信支付（PC / H5 / APP / 小程序）
  - 微信小程序常用接口（手机号、OpenId 等）
  - 用户名 / 手机号 + 短信验证码登录
- 存储与短信
  - 文件存储：阿里云 OSS、腾讯云 COS
  - 短信：阿里云短信、腾讯云短信
- 日志与基础设施
  - 数据库日志、本地文件日志
  - 雪花 ID 生成器（约 139 年可用期）
  - 常用 Helper / 扩展方法（Http、配置、Excel、二维码等）
- 前后端应用
  - `Client.WebAPI`：对外 Web API
  - `Admin.WebAPI` + `Admin.App`：基于 Blazor WASM 的管理后台（用户 / 角色 / 权限）
  - `TaskService`：统一的队列任务 + 定时任务调度中心

---

## 2. 解决方案结构

根目录解决方案：`NetEngine.slnx`

### 2.1 Application（应用层）

- `Application.Interface`：应用服务接口、DTO 接口等
- `Application.Model`：DTO / ViewModel 等模型定义
- `Application.Service`：应用服务实现（编排业务逻辑）

### 2.2 Infrastructure（基础设施层）

- `Common`：通用 Helper、扩展方法、Json 工具、二维码生成等
- `DistributedLock` / `DistributedLock.Redis`：分布式锁抽象 + Redis 实现
- `FileStorage` / `FileStorage.AliCloud` / `FileStorage.TencentCloud`：文件存储抽象 + OSS/COS 实现
- `Logger.DataBase` / `Logger.LocalFile`：数据库、文件日志
- `IdentifierGenerator`：雪花 ID 等标识生成
- `SMS` / `SMS.AliCloud` / `SMS.TencentCloud`：短信抽象 + 两家云厂商实现

### 2.3 Repository（数据访问层）

- `Repository`：基于 EF Core + PostgreSQL 的数据访问层（实体、DbContext、拦截器）
- `Repository.Column` / `Repository.Enum`：字段常量、枚举等元数据
- `Repository.Tool`：专用于迁移的控制台项目（托管 EF Core 工具）

> 默认数据库为 PostgreSQL，可按需切换为 SQL Server / MySQL（见下文“数据库与 EF Core”）。

### 2.4 ProjectCore（核心 WebAPI & 任务基础）

- `WebAPI.Core`
  - Web API 公共基础：JWT、Swagger、跨域、模型验证、健康检查
  - 通用过滤器：缓存、并发/频率限制、ETag、签名校验、RSA 解密、异常处理等
- `TaskService.Core`
  - 队列任务 / 定时任务 的通用基础逻辑
  - Cron 解析、任务注册与同步、执行调度

### 2.5 Presentation（宿主项目）

- `Client.WebAPI`：对外业务 API
- `Admin.WebAPI`：管理后台 API + 静态资源托管
- `Admin.App`：Blazor WASM 管理前端（使用 AntDesign 组件）
- `TaskService`：Worker Service 任务中心（支持 Windows 服务 / systemd）

### 2.6 SourceGenerator（源码生成）

- `SourceGenerator.Core`：源代码生成器（批量注册服务、后台任务等）
- `SourceGenerator.Runtime`：与生成代码配套的运行时组件

### 2.7 InitData（初始化数据）

- 预置初始化 Excel 数据（例如行政区划数据）

---

## 3. 快速开始

### 3.1 环境要求

- .NET 10 SDK（或兼容预览版）
- PostgreSQL 数据库
- Redis
- （可选）阿里云 OSS / 腾讯云 COS、阿里云 / 腾讯云短信

### 3.2 克隆与构建

```bash
git clone https://github.com/xxx/NetEngine.git
cd NetEngine
dotnet restore
dotnet build NetEngine.slnx
```

> `xxx/NetEngine.git` 请替换为你实际仓库地址。

### 3.3 基本配置

以 `Presentation/Client.WebAPI/appsettings.json` 为例：

- `ConnectionStrings:dbConnection`：PostgreSQL 连接字符串
- `ConnectionStrings:redisConnection`：Redis 连接字符串
- `JWT`：Issuer / Audience / PublicKey / PrivateKey / Expiry
- `RSA`：用于敏感字段加解密的公私钥
- `TencentCloudSMS` / `AliCloudSMS`：短信配置
- `TencentCloudFileStorage` / `AliCloudFileStorage`：文件存储配置
- `FileServerUrl`：文件访问 URL 前缀

管理后台后端配置：`Presentation/Admin.WebAPI/appsettings.json`，结构与 Client.WebAPI 基本一致。

### 3.4 启动项目（开发环境）

```bash
# 客户端 WebAPI
dotnet run --project Presentation/Client.WebAPI/Client.WebAPI.csproj

# 管理后台 WebAPI
dotnet run --project Presentation/Admin.WebAPI/Admin.WebAPI.csproj

# 管理后台前端（Blazor WASM）
dotnet run --project Presentation/Admin.App/Admin.App.csproj

# 定时任务 / 队列任务服务（可选）
dotnet run --project Presentation/TaskService/TaskService.csproj
```

- Admin.App 默认使用 `https://localhost:9833/` 作为 API 地址，可在 `Admin.App/Program.cs` 中调整。
- Debug 模式下，TaskService 启动后会在控制台列出所有队列任务 / 定时任务，可通过输入序号启用。

---

## 4. 核心运行时能力

### 4.1 任务调度与队列任务

所有任务都定义在继承 `TaskBase` 的类中（如 `Presentation/TaskService/Tasks`），`TaskService` 启动时会自动扫描并注册。

#### 4.1.1 定义任务类

```csharp
public class DemoTask(ILogger<DemoTask> logger,
                      DatabaseContext db,
                      QueueTaskService queueTaskService) : TaskBase
{
    // 在这里定义定时任务 / 队列任务方法
}
```

#### 4.1.2 周期性定时任务（ScheduleTask）

使用 `[ScheduleTask]` 标记需要周期执行的方法：

```csharp
[ScheduleTask(Name = "ShowTime", Cron = "0/3 * * * * ?")]
public async Task ShowTime()
{
    // 每 3 秒执行一次
}
```

- `Name`：任务唯一名称，对应数据库表 `TTaskSetting.Name`
- `Cron`：6 段 Cron 表达式（含秒），如：
  - `0 0 * * * ?`：每小时整点
  - `0 0 3 * * ?`：每天 3 点
  - `0 */5 * * * ?`：每 5 分钟
- 返回类型：`Task` / `Task<T>` / `void`，禁止 `async void`（会被框架拒绝）
- 带参数任务：如果方法只有一个参数，则实际参数来自 `TTaskSetting.Parameter` 的 JSON，可为同一个任务方法配置多条不同参数 / Cron 的任务实例。

#### 4.1.3 队列任务（QueueTask）

使用 `[QueueTask]` 标记需要排队执行的方法：

```csharp
[QueueTask(Name = "SendSMS", Semaphore = 5, Duration = 5)]
public async Task SendSMS(DtoSendSMS dto)
{
    // 真正的队列任务逻辑
}
```

- `Name`：队列任务名称，入队时的 `name` 必须与之保持一致
- `Semaphore`：并发执行上限（同名任务最多同时执行多少个）
- `Duration`：单次任务预期执行时长（分钟），框架会据此设置分布式锁超时

队列记录参数存放在 `TQueueTask.Parameter` 中，以 JSON 格式序列化；如果方法有返回值且未显式设置 `CallbackParameter`，返回值将作为回调任务参数写入新的队列记录。

#### 4.1.4 在业务中创建队列任务

由 `QueueTaskService` 负责入队，常用两种方式：

1. 在显式事务内创建（与业务数据一致提交）：

```csharp
using var tran = await db.Database.BeginTransactionAsync();

// 业务写库...

queueTaskService.Create(
    name: "SendSMS",
    parameter: new DtoSendSMS { /* ... */ },
    planTime: null,
    callbackName: null,
    callbackParameter: null,
    isChild: false);

await db.SaveChangesAsync();
await tran.CommitAsync();
```

2. 单独创建队列（独立事务）：

```csharp
await queueTaskService.CreateSingleAsync(
    name: "SendSMS",
    parameter: new DtoSendSMS { /* ... */ });
```

参数说明：

- `name`：队列任务名称
- `parameter`：任务参数对象（序列化为 JSON 存入队列表）
- `planTime`：计划执行时间（UTC），为 `null` 表示就绪后可立即被调度
- `callbackName` / `callbackParameter`：可选回调任务名称及参数
- `isChild`：是否标记为当前任务的子任务，用于控制任务树和回调链

Debug 模式下可以参考 `DemoTask.ShowName` 的示例：一个定时任务触发多个队列任务（`ShowName` -> `SendSMS` / `CallPhone` -> `ShowNameSuccess`）。

#### 4.1.5 任务启用与配置

- Debug：通过 TaskService 控制台交互选择要启用的任务
- Release：任务配置存放在 `TTaskSetting` 表中（是否启用 / Cron / 并发数等），`TaskSettingSyncBackgroundService` 会定期同步数据库配置到内存。

---

### 4.2 通用过滤器

以下过滤器均在 `WebAPI.Core.Filters` 中，可通过特性直接应用于控制器或 Action。

#### 4.2.1 缓存过滤器（CacheDataFilter）

- 作用：基于 `IDistributedCache` + 分布式锁，对接口返回结果做数据级缓存。
- 使用示例：

```csharp
[CacheDataFilter(TTL = 60, IsUseToken = true)]
public Task<UserInfo> GetUserInfo() => userService.GetUserInfoAsync();
```

- 关键参数：
  - `TTL`：缓存有效期（秒）
  - `IsUseToken`：是否把 `Authorization` 头参与 CacheKey 生成（避免不同用户串数据）

#### 4.2.2 并发 / 频率限制过滤器（QueueLimitFilter）

- 作用：防止重复提交 / 暴力请求，通过分布式锁对请求排队或限流。
- 使用示例：

```csharp
[QueueLimitFilter(IsBlock = true, IsUseParameter = true, IsUseToken = true, Expiry = 3)]
public Task<IActionResult> SubmitOrder(DtoSubmitOrder dto) => orderService.SubmitAsync(dto);
```

- 关键参数：
  - `IsUseParameter`：是否把请求参数参与锁 Key
  - `IsUseToken`：是否把 Token 参与锁 Key
  - `IsBlock`：有未释放锁时是否直接返回“请勿频繁操作”
  - `Expiry`：锁失效时间（秒），>0 时不会在 Action 结束时立即释放

#### 4.2.3 签名验证过滤器（SignVerifyFilter）

- 作用：在非 Debug 环境下，对请求做强签名校验，用于防篡改和防重放；主要用于 `Admin.WebAPI`。
- 使用方式：
  - 在控制器上统一开启：`[SignVerifyFilter]`
  - 在单个 Action 上跳过：`[SignVerifyFilter(IsSkip = true)]`
- 协议要点：
  - 请求头必须包含：`Token`（签名）、`Time`（毫秒时间戳）
  - `Time` 与服务器时间差不能超过 3 分钟
  - 签名原文包含：JWT 签名部分 + `Time` + 请求路径 + QueryString + 请求体 + Form 字段 + 上传文件内容 SHA256
  - 后端计算 SHA256 与 `Token` 对比，不通过或超时则返回 401

#### 4.2.4 RSA 解密过滤器（RSADecryptFilter）

- 作用：自动解密标记为 `[RSAEncrypted]` 的字符串属性，适合传输敏感字段（密码、手机号等）。
- 配置：`appsettings.json` 中配置 `RSA.PrivateKey`。
- 使用示例：

```csharp
public class LoginRequest
{
    [RSAEncrypted]
    public string Password { get; set; }
}

[RSADecryptFilter]
public Task<IActionResult> Login(LoginRequest request) => authService.LoginAsync(request);
```

#### 4.2.5 ETag 过滤器（ETagFilter）

- 作用：为 GET 请求自动计算 ETag，支持浏览器 / 代理协商缓存。
- 使用：在需要的接口上添加 `[ETagFilter]`，返回 200 且为 `ObjectResult` 时会自动生成 ETag；若客户端携带的 `If-None-Match` 与之匹配，则返回 304。

#### 4.2.6 全局异常过滤器（ExceptionFilter）

- 作用：统一处理 `CustomException`，返回 HTTP 400 + `{ errMsg }`。
- 注册：已在 `WebApplicationBuilderExtension.AddCommonServices` 中全局添加，无需手动配置；业务只需抛出 `CustomException`。

---

### 4.3 分布式锁

项目通过 `DistributedLock` 抽象分布式锁，并在 `DistributedLock.Redis` 中使用 Redis 实现。

#### 4.3.1 注册 Redis 分布式锁

在各宿主项目 `Program.cs` 中已示例：

```csharp
builder.Services.AddRedisLock(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("redisConnection")!;
    options.InstanceName = "lock";
});
```

#### 4.3.2 核心接口

```csharp
public interface IDistributedLock
{
    Task<IDisposable>  LockAsync(string key, TimeSpan expiry = default, int semaphore = 1);
    Task<IDisposable?> TryLockAsync(string key, TimeSpan expiry = default, int semaphore = 1);
}
```

- `key`：锁名（建议带业务前缀，如 `Order:123`）
- `expiry`：锁超时（默认 1 分钟）
- `semaphore`：信号量，用于控制允许同时持有锁的“名额”

#### 4.3.3 使用示例

1. 严格互斥：

```csharp
using (await distLock.LockAsync($"Order:{userId}", TimeSpan.FromMinutes(1)))
{
    // 此处在 userId 维度下串行执行
}
```

2. 尝试获取（不阻塞）：

```csharp
using var handle = await distLock.TryLockAsync("Job:DailyReport", TimeSpan.FromMinutes(10));
if (handle == null)
{
    // 其它节点已在执行，当前节点直接跳过
    return;
}

// 执行任务...
```

3. 有限并发（信号量）：

```csharp
using (await distLock.LockAsync("UploadFile", TimeSpan.FromMinutes(5), semaphore: 5))
{
    // 全局最多 5 个并发上传
}
```

分布式锁已在内部多处使用，例如：`CacheDataFilter` 防止缓存穿透、`QueueLimitFilter` 防重复提交、队列任务执行时的并发控制等。

---

## 5. 数据库与 EF Core

### 5.1 默认：PostgreSQL

仓库默认使用 PostgreSQL，对应 NuGet 包与配置如下：

- 包：`Npgsql.EntityFrameworkCore.PostgreSQL`
- 连接字符串示例：

```text
Host=127.0.0.1;Database=webcore;Username=postgres;Password=123456;Maximum Pool Size=30
```

- EF Core 配置示例：

```csharp
optionsBuilder.UseNpgsql("ConnectionString", o => o.MigrationsHistoryTable("__efmigrationshistory"));
```

`Repository.Tool` 提供了一个 Host 项目方便执行迁移，你可以：

- 使用标准 `dotnet ef` 命令；或
- 在 `Repository.Tool` 中编写迁移代码并运行该项目。

### 5.2 可选数据库（SQL Server / MySQL）

如果需要切换到其他数据库，可参考如下模板：

- SQL Server
  - 包：`Microsoft.EntityFrameworkCore.SqlServer`
  - 连接字符串：
    ```text
    Data Source=127.0.0.1;Initial Catalog=webcore;User ID=sa;Password=123456;Max Pool Size=100;Encrypt=True;TrustServerCertificate=True
    ```
  - EF 配置：
    ```csharp
    optionsBuilder.UseSqlServer("ConnectionString", o => o.MigrationsHistoryTable("__efmigrationshistory"));
    ```

- MySQL（Pomelo）
  - 包：`Pomelo.EntityFrameworkCore.MySql`
  - 连接字符串：
    ```text
    server=127.0.0.1;database=webcore;user id=root;password=123456;maxpoolsize=100
    ```
  - EF 配置：
    ```csharp
    optionsBuilder.UseMySql(
        "ConnectionString",
        new MySqlServerVersion(new Version(8, 0, 29)),
        o => o.MigrationsHistoryTable("__efmigrationshistory"));
    ```

### 5.3 延迟加载（可选）

如需启用 EF Core 延迟加载：

- 安装包：`Microsoft.EntityFrameworkCore.Proxies`
- 在 DbContext 配置中添加：

```csharp
options.UseLazyLoadingProxies();
```

---

## 6. 其他说明

### 6.1 JWT 公私钥生成

本项目默认使用 ECDSA（P-256 曲线）生成 JWT 公私钥：

```csharp
var keyInfo = ECDsa.Create(ECCurve.NamedCurves.nistP256);
var privateKey = Convert.ToBase64String(keyInfo.ExportECPrivateKey());
var publicKey = Convert.ToBase64String(keyInfo.ExportSubjectPublicKeyInfo());
```

生成的 Base64 字符串可直接配置到 `appsettings.json` 的 `JWT.PrivateKey` 和 `JWT.PublicKey`。

### 6.2 行政区划数据

- Excel 数据位于 `InitData` 目录，已完成基础清洗，可直接导入。
- 数据来源：百度地图开放平台  
  https://lbsyun.baidu.com/index.php?title=open/dev-res  
- 当前数据截止时间：`2021-04`

示例导入 SQL（可根据自身表结构调整）：

```sql
insert into RegionArea
select cast(CODE_PROV as int) as Id,
       NAME_PROV as Province,
       '2021-04-01' as CreateTime,
       '0' as IsDelete
from ditu
group by CODE_PROV, NAME_PROV;

insert into RegionCity
select cast(CODE_CITY as int) as Id,
       NAME_CITY as City,
       cast(CODE_PROV as int) as ProvinceId,
       '2021-04-01' as CreateTime,
       '0' as IsDelete
from ditu
group by CODE_CITY, NAME_CITY, CODE_PROV;

insert into RegionProvince
select cast(CODE_COUN as int) as Id,
       NAME_COUN as Area,
       cast(CODE_CITY as int) as CityId,
       '2021-04-01' as CreateTime,
       '0' as IsDelete
from ditu
group by CODE_COUN, NAME_COUN, CODE_CITY;

insert into RegionTown
select cast(CODE_TOWN as int) as Id,
       NAME_TOWN as Town,
       cast(CODE_COUN as int) as AreaId,
       '2021-04-01' as CreateTime,
       '0' as IsDelete
from ditu
group by CODE_TOWN, NAME_TOWN, CODE_COUN;
```

### 6.3 许可协议

本项目基于 MIT License 开源，详情见根目录 `LICENSE` 文件。  
你可以在商业或非商业项目中自由使用、修改和分发本项目，但需保留原始版权声明。

