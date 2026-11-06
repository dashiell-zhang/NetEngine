# NetEngine

基于 .NET 10 的分层解决方案，包含 Web API、Blazor WebAssembly 管理端、任务调度、EF Core、源码生成器以及常用基础设施能力

设计目标：

- 保持分层清晰，避免把业务逻辑堆进宿主层
- 优先复用已有模式，用满足当前需求的简单实现，避免过度设计和无意义包装
- 维持接近 ASP.NET Core 与 EF Core 官方风格的写法

## 主要能力

- JWT 认证、权限控制、请求签名校验与 RSA 字段解密
- PostgreSQL、EF Core、Redis、HybridCache 与本地缓存
- 基于雪花 ID、固定 UTC+8 周期的 PostgreSQL RANGE 分区表迁移与自动维护
- Redis 分布式锁与内存锁
- 文件存储、短信、数据库日志与本地文件日志
- 队列任务与定时任务调度
- 跨进程事件广播，用于多实例缓存失效、配置重载与本地状态刷新
- OpenAI Chat Completions、Responses 与 Anthropic Messages 协议的 LLM 调用能力
- 编译期服务注册、方法代理与 EF Core 辅助代码生成
- Nginx、systemd 与云效流水线部署配置生成

## 快速开始

以下命令默认在仓库根目录执行。首次体验管理后台只需启动本地依赖、`Admin.WebAPI` 和 `Admin.App`，其他宿主按需启动

### 环境要求

- 稳定版 .NET 10.0 SDK，最低 10.0.403；根目录 [global.json](global.json) 允许使用更高的 10.0 SDK 功能带和补丁版本，不选择 .NET 11 或预览版
- PostgreSQL
- Redis；使用 Garnet 等兼容实现时，应验证本项目使用的缓存、Lua 脚本和分布式锁行为
- Docker 可选，用于启动本地 PostgreSQL 与 Redis
- 短信、文件存储和 LLM 的外部配置按所用功能提供

### 启动本地依赖

电脑已安装并启动 Docker 时，可以执行以下命令创建本地开发用的 Redis 和 PostgreSQL 容器：

```powershell
docker run -d --name netengine-redis -p 127.0.0.1:6379:6379 redis:8
docker run -d --name netengine-postgres -e TZ=Asia/Shanghai -e POSTGRES_DB=webcore -e POSTGRES_PASSWORD=123456 -p 127.0.0.1:5432:5432 -v netengine-postgres-data:/var/lib/postgresql postgres:18
```

示例固定镜像主版本，端口只向本机开放，PostgreSQL 数据保存在命名卷中。PostgreSQL 18 的卷挂载位置及初始化变量说明见 [官方镜像文档](https://hub.docker.com/_/postgres)。等待数据库初始化完成后再执行迁移

容器已创建但处于停止状态时，使用 `docker start netengine-redis netengine-postgres`，不必重复执行 `docker run`

已有可用的 PostgreSQL 和 Redis 服务时，可以直接使用，跳过容器创建步骤

使用其他安装方式、端口、账号或密码时，需要同步修改项目中的数据库和 Redis 连接字符串

仓库本地开发示例使用的 PostgreSQL 连接信息为：

```text
Host=127.0.0.1;Database=webcore;Username=postgres;Password=123456
```

配置文件位置：

| 配置 | 位置 |
|---|---|
| 管理端 API 的数据库与 Redis | `Presentation/Admin.WebAPI/appsettings*.json` |
| 客户端 API 的数据库与 Redis | `Presentation/Client.WebAPI/appsettings*.json` |
| 任务宿主的数据库与 Redis | `Presentation/TaskService/appsettings*.json` |
| 迁移工具的数据库连接 | [Repository.Tool/appsettings.json](Repository.Tool/appsettings.json) |

三个宿主使用 `ConnectionStrings:dbConnection`、`dbReadConnection` 和 `redisConnection`。本地 `dbReadConnection` 可留空并回退到主库，Redis 默认连接本机。`Development` 环境会加载 `appsettings.Development.json`，核对配置时同时检查基础文件与开发环境文件

日志、事件和 ID 协调可分别配置独立连接，默认目标和共享资源配置见 [业务模块与数据库接入](Docs/DatabaseModules.md)。迁移工具固定从 Repository.Tool/appsettings.json 读取 Business、Log、EventBus 目标，不读取 WebAPI 配置或环境变量覆盖；修改宿主配置后还需确认工具指向对应数据库。读库的完整配置见 [数据库读写分离](Docs/DatabaseReadWriteSeparation.md)

### HTTPS 开发证书

本地启动配置使用 HTTPS。首次运行前创建并信任开发证书：

```powershell
dotnet dev-certs https --trust
```

浏览器仍提示证书不受信任时，按 [开发证书文档](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-dev-certs) 检查对应平台和浏览器的信任配置

### 构建

```powershell
dotnet build NetEngine.slnx
```

`dotnet build` 默认包含 NuGet 还原。若提示缺少 WebAssembly workload，可先执行 `dotnet workload restore Presentation/Admin.App/Admin.App.csproj`，再重新构建；命令说明见 [workload restore](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-workload-restore)

### 创建数据库结构

使用 Visual Studio 程序包管理控制台时，Repository.Tool 已引用 EF 工具包，无需单独安装 dotnet-ef，可直接按下方控制台步骤操作。使用 dotnet 命令行时，工具与项目保持 EF Core 10 主版本；首次安装：

```powershell
dotnet tool install --global dotnet-ef --version "10.*"
```

已安装时先用 `dotnet ef --version` 检查，需要更新时执行 `dotnet tool update --global dotnet-ef --version "10.*"`。以后升级 EF Core 主版本时，同步调整这里的主版本，项目版本以 [Repository.Tool.csproj](Repository.Tool/Repository.Tool.csproj) 为准。工具用法见 [EF Core CLI 文档](https://learn.microsoft.com/en-us/ef/core/cli/dotnet)，版本通配符见 [工具安装文档](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-install#options)

工具读取项目内的 [appsettings.json](Repository.Tool/appsettings.json)，业务、日志和数据库事件分别管理迁移与历史表。当前模板未包含迁移文件或模型快照，首次初始化空数据库时，需要先生成对应的初始迁移

在 Visual Studio 中将 Repository.Tool 设为启动项目和程序包管理控制台的默认项目，使用 Debug。核对配置和空数据库目标后，按下面的顺序操作，每次生成迁移后先检查内容，再执行对应的数据库更新：

```powershell
Add-Migration Init1 -Context DatabaseContext -OutputDir Migrations
Update-Database -Context DatabaseContext

Add-Migration Init2 -Context LogDatabaseContext -OutputDir Migrations/LogDatabase
Update-Database -Context LogDatabaseContext

# 使用数据库事件后端时执行，Redis 后端可以省略
Add-Migration Init3 -Context EventBusDatabaseContext -OutputDir Migrations/EventBusDatabase
Update-Database -Context EventBusDatabaseContext
```

使用命令行时，业务模型的等价初始化命令为，生成后同样先检查迁移内容：

```powershell
dotnet ef migrations add Init1 --project Repository.Tool --startup-project Repository.Tool --context Repository.DatabaseContext --configuration Debug --output-dir Migrations
dotnet ef database update --project Repository.Tool --startup-project Repository.Tool --context Repository.DatabaseContext --configuration Debug
```

命令行更新日志和事件时，分别指定对应 Context。完整的 Visual Studio 初始迁移、后续模型变更命令及历史表说明见 [数据库工具](Docs/RepositoryTool.md#visual-studio-程序包管理控制台)。仅更新业务模型不会创建组件表

已经存在迁移文件时，使用已有迁移更新数据库，不要重复生成初始迁移。数据库已有业务表但没有对应迁移历史时，需要先确认迁移基线，不能直接按空库流程执行。包含分区表的迁移按 [PostgreSQL 分区表](Docs/PostgreSqlPartitionTable.md) 核对生成 SQL

### 初始化并启动管理后台

启动管理端 API：

```powershell
dotnet run --project Presentation/Admin.WebAPI/Admin.WebAPI.csproj --launch-profile Project
```

浏览器访问 [管理端 API Swagger](https://localhost:9833/swagger)，在 Swagger 中执行 `POST /Authorize/InitData`，初始化管理员、角色和权限基础数据

该接口只允许在 `Development` 环境执行，数据来源为 [AuthorizeInitData.json](InitData/AuthorizeInitData.json)。重复调用会重新应用文件中的管理员资料、密码及权限基础配置，不只是补充缺失记录；管理员密码已修改时，不要把该接口当作日常启动步骤

保持 `Admin.WebAPI` 运行，并在另一个终端启动管理端：

```powershell
dotnet run --project Presentation/Admin.App/Admin.App.csproj --launch-profile Project
```

浏览器访问 [管理后台](https://localhost:16701)，默认初始化账号为 `admin` / `123456`；修改了初始化文件时使用对应账号。本节示例密码仅用于本地开发

`Admin.App` 的 API 地址当前写在 [Program.cs](Presentation/Admin.App/Program.cs) 中，默认为 `https://localhost:9833/`。调整 API 地址时，应同步修改该值并检查 API 的 CORS 来源配置

上述 `Project` 启动配置设置了 `Development` 环境和本地端口。Debug / Release 是编译配置，与 Development / Production 运行环境分别生效；关闭启动配置或改变环境后，地址和 Swagger 的可用性可能不同

### 其他宿主

需要客户端 API 或后台任务时，在独立终端中按需启动：

```powershell
dotnet run --project Presentation/Client.WebAPI/Client.WebAPI.csproj --launch-profile Project
dotnet run --project Presentation/TaskService/TaskService.csproj --launch-profile TaskService
```

客户端 API 的开发入口为 [Swagger](https://localhost:9801/swagger)。TaskService 的 Debug 构建需要在控制台选择启用任务，非 Debug 构建由数据库任务配置控制，详见 [TaskService](Docs/TaskService.md)

多个宿主按顺序完成构建与启动，避免多个独立构建进程争用源码生成器输出。两个 WebAPI 宿主均提供 `/healthz`；检查通过表示当前业务缓存、已注册数据库连接和所选事件后端连接可用，不验证表结构、迁移状态或全部业务功能

## 使用文档

详细使用说明统一放在 [Docs](Docs/README.md)：

| 文档 | 主要内容 |
|---|---|
| [架构与项目边界](Docs/Architecture.md) | 分层职责、依赖方向、宿主引用与项目拆分原则 |
| [框架架构设计图](Docs/ArchitectureDiagrams.md) | 分层、请求流程、存储归属、任务、缓存锁与部署关系 |
| [WebAPI 公共能力](Docs/WebAPI.md) | 公共启动链路、配置、CORS、认证、Swagger 与健康检查 |
| [WebAPI 过滤器](Docs/WebAPIFilters.md) | 异常、缓存、ETag、并发限制、RSA 解密和签名校验 |
| [源码生成器](Docs/SourceGenerator.md) | 自动 DI、后台服务、AutoProxy 与 EF Core 代码生成 |
| [业务模块与数据库接入](Docs/DatabaseModules.md) | 模型归属、同进程多业务库、本地契约调用与共享资源配置 |
| [数据库工具](Docs/RepositoryTool.md) | 默认配置、Visual Studio 迁移和 JSON 索引差异 |
| [ID 生成器](Docs/IdentifierGenerator.md) | 共享 Redis 节点分配、租约续期与发号边界 |
| [数据库读写分离](Docs/DatabaseReadWriteSeparation.md) | 读写上下文用法、读库连接配置、一致性和多读库方案 |
| [数据库日志](Docs/DatabaseLogger.md) | 独立日志库、查询接入、按 Project 清理与文件恢复 |
| [本地文件日志](Docs/LocalFileLogger.md) | 文件日志接入、缓冲、恢复与清理 |
| [PostgreSQL 分区表](Docs/PostgreSqlPartitionTable.md) | 实体注解、UTC+8 周期单位、Migration 建表与子分区维护 |
| [TaskService](Docs/TaskService.md) | 定时任务、队列任务、调度、回调、子任务与重试 |
| [EventBus 跨进程广播](Docs/EventBus.md) | 适用场景、多实例缓存刷新示例、事件发布与监听、Redis 与数据库后端配置 |
| [分布式锁](Docs/DistributedLock.md) | Redis 锁、内存锁、并发信号量与租约续期 |
| [LLM 调用](Docs/LLM.md) | 模型配置、提示词模板、普通调用与流式调用 |
| [部署配置生成器](Docs/Deployment.md) | Nginx、systemd 与云效流水线配置生成 |

## 项目结构

解决方案文件为 `NetEngine.slnx`

| 目录 | 职责 |
|---|---|
| `Application` | 应用层接口、DTO、请求与返回模型、通用及宿主特化应用服务 |
| `Repository` | 当前业务实体、上下文及本地 QueueTask、TaskSetting 映射 |
| `Infrastructure/Repository.Core` | 共用 PostgreSQL/EF 机制、基础实体与分区能力 |
| `Infrastructure/QueueTask.Database` | 可绑定业务上下文的队列、任务配置与管理存储 |
| `Repository.Tool` | EF Core 迁移与数据库工具宿主 |
| `Infrastructure` | 缓存、锁、文件、短信、日志、LLM、ID 生成等基础设施实现 |
| `ProjectCore` | WebAPI 与 TaskService 宿主共用能力 |
| `Presentation` | Client API、Admin API、Blazor 管理端与任务宿主 |
| `SourceGenerator` | 编译期源码生成器及生成代码运行时支持 |
| `InitData` | 管理员、角色、权限等初始化数据文件 |
| `Deployment` | 部署配置生成器、模板、输入配置与生成产物 |
| `Docs` | 公共能力和工具的详细使用文档 |

整体依赖方向为 `Presentation -> Application -> Repository / Infrastructure`，公共宿主能力位于 `ProjectCore`。详细职责、宿主引用关系和拆分原则见 [架构与项目边界](Docs/Architecture.md)

Agent 的修改流程、代码风格和验证规则见 [AGENTS.md](AGENTS.md)

## 许可协议

本项目基于 MIT License 开源，详见 [LICENSE](LICENSE)
