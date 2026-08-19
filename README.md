# NetEngine

基于 .NET 10 的分层解决方案，包含 Web API、Blazor WebAssembly 管理端、任务调度、EF Core、源码生成器以及常用基础设施能力

设计目标：

- 保持分层清晰，避免把业务逻辑堆进宿主层
- 尽量复用已有模式，减少无意义包装
- 维持接近 ASP.NET Core 与 EF Core 官方风格的写法

## 主要能力

- JWT 认证、权限控制、请求签名校验与 RSA 字段解密
- PostgreSQL、EF Core、Redis、HybridCache 与本地缓存
- Redis 分布式锁与内存锁
- 文件存储、短信、数据库日志与本地文件日志
- 队列任务与定时任务调度
- OpenAI Chat Completions、Responses 与 Anthropic Messages 协议的 LLM 调用能力
- 编译期服务注册、方法代理与 EF Core 辅助代码生成
- Nginx、systemd 与云效流水线部署配置生成

## 快速开始

### 环境要求

- .NET 10 SDK
- PostgreSQL
- Redis 或 Garnet
- 可选使用 Docker 启动 PostgreSQL 与 Redis
- 可选的云厂商配置，如短信、文件存储和 LLM Provider

### 启动本地依赖

电脑已安装 Docker 时，可以执行以下命令启动本地开发使用的 Redis 和 PostgreSQL：

```powershell
docker run -d --name redis -p 6379:6379 redis:latest redis-server --save ""
docker run -d --name postgres -e TZ=Asia/Shanghai -e POSTGRES_PASSWORD=123456 -p 5432:5432 postgres:latest
```

也可以直接安装 Windows 版 Redis/Garnet 与 PostgreSQL，不要求必须使用 Docker

使用其他安装方式、端口、账号或密码时，需要同步修改项目中的数据库和 Redis 连接字符串

仓库默认 PostgreSQL 连接信息为：

```text
Host=127.0.0.1;Database=webcore;Username=postgres;Password=123456
```

配置文件位置：

| 配置 | 位置 |
|---|---|
| PostgreSQL | `Presentation/Admin.WebAPI/appsettings*.json` |
| PostgreSQL | `Presentation/Client.WebAPI/appsettings*.json` |
| PostgreSQL | `Presentation/TaskService/appsettings*.json` |
| PostgreSQL 迁移工具 | `Repository.Tool/Program.cs` |
| Redis | `Presentation/Admin.WebAPI/appsettings*.json` |
| Redis | `Presentation/Client.WebAPI/appsettings*.json` |
| Redis | `Presentation/TaskService/appsettings*.json` |

仓库默认 Redis 连接字符串可以直接连接上面的 Redis 容器

### 构建

```powershell
dotnet restore
dotnet build NetEngine.slnx
```

### 创建数据库结构

如果尚未安装 EF Core 命令行工具，可以先执行：

```powershell
dotnet tool install --global dotnet-ef
```

当前项目还没有迁移文件时，先在解决方案根目录生成初始迁移：

```powershell
dotnet ef migrations add InitialCreate --project Repository.Tool --startup-project Repository.Tool
```

然后根据迁移创建或更新数据库：

```powershell
dotnet ef database update --project Repository.Tool --startup-project Repository.Tool
```

已经存在迁移文件时，只需要执行数据库更新命令

### 初始化并启动管理后台

启动管理端 API：

```powershell
dotnet run --project Presentation/Admin.WebAPI/Admin.WebAPI.csproj
```

浏览器访问 `https://localhost:9833/swagger`，在 Swagger 中执行 `POST /Authorize/InitData`，初始化管理员、角色和权限基础数据

该接口只允许在 `Development` 环境执行，可以重复调用并更新初始化数据

保持 `Admin.WebAPI` 运行，并在另一个终端启动管理端：

```powershell
dotnet run --project Presentation/Admin.App/Admin.App.csproj
```

浏览器访问 `https://localhost:16701`，使用 `admin` / `123456` 登录

### 其他宿主

```powershell
dotnet run --project Presentation/Client.WebAPI/Client.WebAPI.csproj
dotnet run --project Presentation/TaskService/TaskService.csproj
```

## 使用文档

详细使用说明统一放在 [Docs](Docs/README.md)：

| 文档 | 主要内容 |
|---|---|
| [架构与项目边界](Docs/Architecture.md) | 分层职责、依赖方向、宿主引用与项目拆分原则 |
| [WebAPI 公共能力](Docs/WebAPI.md) | 公共启动链路、配置、CORS、认证、Swagger 与健康检查 |
| [WebAPI 过滤器](Docs/WebAPIFilters.md) | 异常、缓存、ETag、并发限制、RSA 解密和签名校验 |
| [源码生成器](Docs/SourceGenerator.md) | 自动 DI、后台服务、AutoProxy 与 EF Core 代码生成 |
| [数据库读写分离](Docs/DatabaseReadWriteSeparation.md) | 读写上下文用法、读库连接配置、一致性和多读库方案 |
| [TaskService](Docs/TaskService.md) | 定时任务、队列任务、调度、回调、子任务与重试 |
| [分布式锁](Docs/DistributedLock.md) | Redis 锁、内存锁、并发信号量与租约续期 |
| [LLM 调用](Docs/LLM.md) | 模型配置、提示词模板、普通调用与流式调用 |
| [部署配置生成器](Docs/Deployment.md) | Nginx、systemd 与云效流水线配置生成 |

## 项目结构

解决方案文件为 `NetEngine.slnx`

| 目录 | 职责 |
|---|---|
| `Application` | 应用层接口、DTO、请求与返回模型、通用及宿主特化应用服务 |
| `Repository` | EF Core 实体、上下文、映射、拦截器、持久化逻辑与迁移工具 |
| `Infrastructure` | 缓存、锁、文件、短信、日志、LLM、ID 生成等基础设施实现 |
| `ProjectCore` | WebAPI 与 TaskService 宿主共用能力 |
| `Presentation` | Client API、Admin API、Blazor 管理端与任务宿主 |
| `SourceGenerator` | 编译期源码生成器及生成代码运行时支持 |
| `InitData` | 管理员、角色、权限等初始化数据文件 |
| `Deployment` | 部署配置生成器、模板、输入配置与生成产物 |
| `Docs` | 公共能力和工具的详细使用文档 |

整体依赖方向为 `Presentation -> Application -> Repository / Infrastructure`，公共宿主能力位于 `ProjectCore`。详细职责、宿主引用关系和拆分原则见 [架构与项目边界](Docs/Architecture.md)

## 许可协议

本项目基于 MIT License 开源，详见根目录 `LICENSE`
