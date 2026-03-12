# NetEngine

基于 .NET 10 的分层解决方案，包含 Web API、Blazor WASM 管理端、任务调度、EF Core、源码生成器以及常用基础设施能力

设计目标：

- 保持分层清晰，避免把业务逻辑堆进宿主层
- 尽量复用已有模式，减少无意义包装
- 维持接近 ASP.NET Core / EF Core 官方风格的写法

## 项目现状

当前解决方案根文件为 `NetEngine.slnx`

### Application

- `Application.Interface`
  - 放跨层公共抽象
  - 当前主要承载 `IUserContext` 这类被 `ProjectCore` 和应用服务共同依赖的接口
- `Application.Model`
  - 放 DTO、请求模型、返回模型、配置模型
  - `Admin.App` 直接引用这一层以复用前后端契约
- `Application.Service`
  - 放通用应用服务实现
  - 当前主要包含用户、站点、基础能力、授权、支付、消息、任务中心等服务
- `Application.Service.LLM`
  - 放 LLM 相关应用服务实现
  - 已从 `Application.Service` 独立拆出，用于按宿主收敛注册范围
  - 当前包含 `LlmAppService`、`LlmConversationManageService`、`LlmInvokeService`
- `Application.Service.SMS`
  - 放短信发送相关应用服务实现
  - 已从 `MessageService` 中独立拆出，用于避免通过可空依赖兜底
  - 当前包含 `SmsService`

### Repository

- `Repository`
  - EF Core 实体、`DatabaseContext`、数据库映射、拦截器、持久化逻辑
- `Repository.Tool`
  - 迁移和数据库工具宿主

### Infrastructure

- `Common`
  - 通用工具、扩展方法、Json 等基础能力
- `DistributedLock` / `DistributedLock.Redis` / `DistributedLock.InMemory`
  - 分布式锁抽象、Redis 实现、内存实现
- `FileStorage` / `FileStorage.AliCloud` / `FileStorage.TencentCloud`
  - 文件存储抽象与云厂商实现
- `SMS` / `SMS.AliCloud` / `SMS.TencentCloud`
  - 短信抽象与云厂商实现
- `Logger.DataBase` / `Logger.LocalFile`
  - 数据库日志与本地文件日志
- `IdentifierGenerator`
  - ID 生成能力
- `LLM`
  - LLM 客户端抽象、工厂与 OpenAI Compatible Provider 实现

### ProjectCore

- `WebAPI.Core`
  - Web API 宿主公共能力
  - 包含认证、Swagger、过滤器、健康检查、用户上下文接入等
- `TaskService.Core`
  - 任务宿主公共能力
  - 包含队列任务、定时任务、初始化和同步逻辑

### Presentation

- `Client.WebAPI`
  - 对外 API 宿主
  - 当前引用 `Application.Service` 和 `Application.Service.LLM`
- `Admin.WebAPI`
  - 管理端 API 宿主
  - 当前引用 `Application.Service` 和 `Application.Service.LLM`
- `Admin.App`
  - Blazor WebAssembly 管理前端
  - 当前直接引用 `Application.Model`
- `TaskService`
  - Worker Service 任务宿主
  - 当前引用 `Application.Service` 和 `Application.Service.SMS`
  - 不再引用 `Application.Service.LLM`

### SourceGenerator

- `SourceGenerator.Core`
  - 编译期源码生成器
- `SourceGenerator.Runtime`
  - 生成代码运行时支持

### InitData

- 初始化数据文件

## 当前依赖边界

当前结构更接近下面这套关系：

- `Presentation` 调用 `Application`
- `Application` 依赖 `Repository` 和 `Infrastructure`
- `ProjectCore` 依赖 `Application.Interface` 与 `Repository`
- `Admin.App` 只复用 `Application.Model`

这套边界的重点是：

- `Application.Interface` 不是“所有服务都要有接口”的接口层
- 它更适合承载跨宿主、跨公共层要依赖的抽象
- 应用服务可以直接以具体类注入
- 只有宿主特化明显、依赖特征强的应用域才值得继续拆项目

## 服务注册方式

仓库通过源码生成器自动生成 DI 注册扩展

- `BatchRegisterServices()`
  - 注册当前启动项目及其所引用程序集内标记了 `[RegisterService]` 的服务
- `BatchRegisterBackgroundServices()`
  - 注册当前启动项目及其所引用程序集内符合条件的后台服务

这意味着：

- 服务是否被宿主注册，取决于宿主是否引用了对应类库
- 将宿主特化服务拆成独立项目，可以天然收敛注册范围

`Application.Service.LLM` 的拆分就是基于这个原则完成的

## 主要能力

- JWT 认证与权限控制
- 请求签名校验与 RSA 字段解密
- PostgreSQL + EF Core
- Redis 分布式缓存、HybridCache、本地缓存
- Redis 分布式锁与内存锁
- 文件存储抽象与阿里云 / 腾讯云实现
- 短信抽象与阿里云 / 腾讯云实现
- 数据库日志与本地文件日志
- 队列任务与定时任务调度
- OpenAI Compatible 风格的 LLM 调用能力

## LLM 现状

当前 LLM 相关能力分成两层：

- `Infrastructure/LLM`
  - 提供 `ILlmClientFactory`、Provider 注册扩展、客户端实现
- `Application/Application.Service.LLM`
  - 提供基于业务语义的应用服务
  - `LlmAppService` 负责 LLM 应用配置管理
  - `LlmConversationManageService` 负责对话记录查询
  - `LlmInvokeService` 负责按 `LlmApp.Code` 发起调用

当前宿主引用关系：

- `Admin.WebAPI`：引用 `Application.Service.LLM`
- `Client.WebAPI`：引用 `Application.Service.LLM`
- `TaskService`：不引用 `Application.Service.LLM`

这样可以避免“某个宿主根本不用 LLM 应用服务，却因为全量注册导致启动时报缺失依赖”

## 快速开始

### 环境要求

- .NET 10 SDK
- PostgreSQL
- Redis
- 可选的云厂商配置，如短信、文件存储、LLM Provider

### 构建

```powershell
dotnet restore
dotnet build NetEngine.slnx
```

### 常用启动命令

```powershell
dotnet run --project Presentation/Client.WebAPI/Client.WebAPI.csproj
dotnet run --project Presentation/Admin.WebAPI/Admin.WebAPI.csproj
dotnet run --project Presentation/Admin.App/Admin.App.csproj
dotnet run --project Presentation/TaskService/TaskService.csproj
```

### 健康检查

- `Client.WebAPI`：`/healthz`
- `Admin.WebAPI`：`/healthz`

## 配置说明

常见配置位于各宿主的 `appsettings.json` 与 `appsettings.Development.json`

重点配置项包括：

- `ConnectionStrings:dbConnection`
- `ConnectionStrings:redisConnection`
- `JWT`
- `RSA`
- `LLM:Providers`
- `AliCloudFileStorage` / `TencentCloudFileStorage`
- `FileServerUrl`

安全提示：

- 仓库内配置默认按本地开发或示例值理解
- 不要把真实密钥、真实连接串、真实密码提交进仓库

## TaskService 说明

- 任务宿主位于 `Presentation/TaskService`
- 公共任务能力位于 `ProjectCore/TaskService.Core`
- Debug 模式下启动后会进入交互式启用流程
- 队列任务和定时任务优先复用现有 Builder、Attribute 与注册方式

## Admin.App 说明

- 项目位于 `Presentation/Admin.App`
- 目标框架为 `net10.0-browser`
- 当前直接复用 `Application.Model` 中的 DTO 与请求模型

## 数据库说明

- 默认数据库为 PostgreSQL
- EF Core 核心实现位于 `Repository`
- 涉及结构调整时，应同步关注实体、`DatabaseContext`、映射与调用点
- 如需迁移工具支持，优先看 `Repository.Tool`

## 许可协议

本项目基于 MIT License 开源，详见根目录 `LICENSE`
