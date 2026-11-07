# 架构与项目边界

本文说明 NetEngine 的分层职责、依赖方向、宿主引用关系与项目拆分原则。新增功能或调整项目引用前，应先根据这里的边界确认代码应该放在哪一层

框架整体关系与运行流程可先阅读 [框架架构设计图](ArchitectureDiagrams.md)

## 总体结构

| 层级 | 目录 | 主要职责 |
|---|---|---|
| 表现与宿主层 | `Presentation` | 请求接入、页面展示、宿主组装、中间件和依赖注入入口 |
| 应用层 | `Application` | 业务逻辑、业务编排、应用服务和跨层数据契约 |
| 业务数据访问层 | `Repository` | 本模块业务实体、上下文、映射及本地任务持久化 |
| 基础设施层 | `Infrastructure` | 共用数据库机制，以及缓存、锁、日志、事件、队列存储、文件、短信、LLM 等组件 |
| 公共宿主层 | `ProjectCore` | WebAPI 与 TaskService 的公共启动和运行能力 |
| 编译期工具 | `SourceGenerator` | 服务注册、代理、EF Core 辅助代码等源码生成能力 |

主要依赖方向：

```text
Presentation ──> Application ──> Repository
       │               └───────> Infrastructure
       └───────> ProjectCore ──> 共享契约 / 基础设施组件

Admin.App ──> Application.Model

业务 Repository / Logger.Database / EventBus.Database / QueueTask.Database
       └───────> Infrastructure/Repository.Core
```

表现层负责接入与组装，业务判断应进入应用层；业务数据访问进入所属模块的 Repository，独立组件的存储由组件管理，共用 EF/PostgreSQL 机制进入 Repository.Core。基础设施组件不反向依赖业务 Repository 或 ProjectCore

## Application

| 项目 | 职责 |
|---|---|
| `Application.Interface` | WebAPI 与任务宿主使用的应用服务契约，以及跨模块共享的应用抽象 |
| `Application.Model` | DTO、请求模型、返回模型和配置模型，也是管理前端复用的契约层 |
| `Application.Service` | 用户、站点、授权、支付、消息、任务中心等通用应用服务 |
| `Application.Service.LLM` | LLM 应用配置、对话管理和模型调用服务 |
| `Application.Service.SMS` | 短信发送相关应用服务 |

WebAPI 与任务宿主使用的应用服务在 `Application.Interface` 中定义接口，Controller、权限适配服务、请求处理器及任务类通过接口调用业务用例，不直接注入具体应用服务。接口按业务能力组织，公开调用方需要的操作，不照搬实现类内部的辅助方法，也不要求每条 HTTP 路由对应一个独立接口

接口的参数与返回值使用 `Application.Model` 中的 DTO 或通用 .NET 类型，不暴露 EF 实体、数据库上下文、HTTP 上下文及基础设施客户端类型。具体实现位于 `Application.Service` 或对应特化类库；应用层内部辅助类按实际依赖边界决定是否需要接口

```text
Controller / 请求适配器 / 任务类 → Application.Interface + Application.Model
Application.Service / 特化实现 → Application.Interface + Repository / Infrastructure
WebAPI / TaskService 宿主启动入口 → 选择实现类库并完成依赖注入注册
```

Controller 直接通过 `ControllerBase.User` 和 WebAPI.Core 的 Claims 扩展解析身份，当前认证用户通过 `actorUserId` 或 `targetUserId` 明确传入 Application Service。应用服务不读取 HTTP 请求上下文

`Application.Service`、`Application.Service.LLM` 和 `Application.Service.SMS` 显式引用 `Application.Interface` 和 `Application.Model`。两个 WebAPI 及 TaskService 同时引用契约与所需的实现项目：业务调用依赖接口，实现项目用于宿主组装及生成服务注册

## Repository

- 业务 `Repository` 负责本模块实体、写读上下文和业务持久化，公共机制位于 `Infrastructure/Repository.Core`，独立组件拥有自己的内部模型
- `Repository.Tool` 是迁移和数据库工具宿主
- 数据库结构调整应同步检查实体、上下文、映射、拦截器及调用点

## Infrastructure

Infrastructure 按能力拆分为抽象、公共实现或厂商实现，当前主要包括：

- `DistributedLock`、`DistributedLock.Redis`、`DistributedLock.InMemory`
- `EventBus`、`EventBus.Redis`、`EventBus.Database`，公共广播运行能力及可替换存储；数据库后端拥有独立的 EventBusDatabaseContext，按宿主传入的连接访问主库，不依赖业务 Repository
- `FileStorage`、`FileStorage.AliCloud`、`FileStorage.TencentCloud`
- `SMS`、`SMS.AliCloud`、`SMS.TencentCloud`
- `Logger.Database`、`Logger.LocalFile`；数据库日志拥有独立写读上下文和查询入口
- `Repository.Core`：公共实体约定、PostgreSQL 分区、上下文注册和连接检查
- `QueueTask.Database`：可复用队列、TaskSetting、入队、管理及存储操作，表随各模块业务库部署
- `IdentifierGenerator`
- `LLM`
- `Common`

应用层可以依赖这些能力的公共抽象，但不应把具体厂商参数或协议细节暴露给 Controller、页面或应用层 DTO

## ProjectCore

| 项目 | 职责 |
|---|---|
| `WebAPI.Core` | 认证、授权、Swagger、过滤器、健康检查、用户上下文和公共中间件 |
| `TaskService.Core` | 队列任务、定时任务、任务初始化与同步 |

ProjectCore 负责多个宿主共同需要的运行能力，不承载具体业务逻辑，不依赖具体业务 Repository 或具体 Application.Service。WebAPI.Core 不默认注册业务数据库检查；TaskService.Core 通过队列组件操作宿主选定的业务库，初始化仅扫描显式选择的任务类型

## Presentation 与宿主引用

| 宿主 | 类型 | 主要应用层引用 |
|---|---|---|
| `Client.WebAPI` | 对外 Web API | `Application.Interface`、`Application.Service`、`Application.Service.LLM` |
| `Admin.WebAPI` | 管理端 Web API | `Application.Interface`、`Application.Service`、`Application.Service.LLM` |
| `Admin.App` | Blazor WebAssembly | `Application.Model` |
| `TaskService` | Worker Service | `Application.Interface`、`Application.Service`、`Application.Service.SMS` |

`TaskService` 不引用 `Application.Service.LLM`。宿主只引用实际需要的应用类库，可以避免源码生成注册时把无关服务及其基础设施依赖带入当前宿主

## 服务注册与项目引用

仓库通过源码生成器提供以下注册入口：

- `BatchRegisterServices()` 注册当前启动项目及其引用程序集内标记了 `[RegisterService]` 的服务
- `BatchRegisterBackgroundServices()` 注册当前启动项目及其引用程序集内符合条件的后台服务

服务是否进入宿主的注册范围，取决于宿主是否引用了对应类库。因此，项目引用也是服务边界的一部分

应用服务实现通过 `[RegisterService(typeof(IUserService), Lifetime = ServiceLifetime.Scoped)]` 等特性明确注册到接口，消费方也使用接口注入。仅管理宿主需要的日志查询适配由 `Admin.WebAPI` 在接入日志查询组件后显式注册 `ILogManageService` 与 `LogManageService`，不进入其他宿主的自动注册范围

完整注册规则、AutoProxy 和 EF Core 生成能力见 [源码生成器](SourceGenerator.md)

## 何时拆分应用项目

满足下面的情况时，可以将某个应用域从 `Application.Service` 拆成独立项目：

- 只被部分宿主使用
- 依赖特定基础设施能力
- 留在通用应用项目会迫使其他宿主注册无关依赖
- 能形成职责清楚且稳定的应用域

`Application.Service.LLM` 与 `Application.Service.SMS` 是当前参考实现

不要仅为了目录整齐就拆出新项目。如果服务被多数宿主共同使用，并且没有明显的特化依赖，应继续放在 `Application.Service`

## 新增功能放置建议

| 需求 | 优先位置 |
|---|---|
| 新增请求或返回模型 | `Application.Model` |
| 新增 WebAPI 与任务宿主使用的应用服务接口 | `Application.Interface`，按业务能力划分命名空间 |
| 新增业务逻辑或业务编排 | `Application.Service` 或对应的宿主特化应用项目 |
| 新增业务实体、查询或持久化逻辑 | 所属模块的 `Repository` |
| 日志、事件等组件的实体与存储 | 对应 `Infrastructure` 组件；共用数据库机制放入 `Repository.Core` |
| 跨进程广播事件 | 公共能力、Redis 和数据库后端在 `Infrastructure`，业务事件与监听在 Application；事件组件拥有独立上下文，宿主明确选择事件连接 |
| 接入缓存、云平台或第三方服务 | `Infrastructure` |
| 新增 Controller 或宿主启动配置 | `Presentation` |
| 多个 WebAPI 宿主共用的 HTTP 能力 | `ProjectCore/WebAPI.Core` |
| 定时任务或队列任务执行、调度能力 | `ProjectCore/TaskService.Core` |
| 队列入队、任务配置与管理存储 | `Infrastructure/QueueTask.Database`，绑定所属业务上下文 |
| 编译期自动生成能力 | `SourceGenerator` |

同进程多业务库、跨模块本地调用及 Redis 绑定方式见 [业务模块与数据库接入](DatabaseModules.md)

修改前还应搜索同层现有实现，优先沿用当前模式，避免在表现层直接编写业务逻辑或数据库访问代码
