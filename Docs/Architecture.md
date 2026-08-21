# 架构与项目边界

本文说明 NetEngine 的分层职责、依赖方向、宿主引用关系与项目拆分原则。新增功能或调整项目引用前，应先根据这里的边界确认代码应该放在哪一层

## 总体结构

| 层级 | 目录 | 主要职责 |
|---|---|---|
| 表现与宿主层 | `Presentation` | 请求接入、页面展示、宿主组装、中间件和依赖注入入口 |
| 应用层 | `Application` | 业务逻辑、业务编排、应用服务和跨层数据契约 |
| 数据访问层 | `Repository` | EF Core 实体、上下文、映射、拦截器和持久化逻辑 |
| 基础设施层 | `Infrastructure` | 缓存、锁、文件、短信、日志、LLM 等外部能力接入 |
| 公共宿主层 | `ProjectCore` | WebAPI 与 TaskService 的公共启动和运行能力 |
| 编译期工具 | `SourceGenerator` | 服务注册、代理、EF Core 辅助代码等源码生成能力 |

主要依赖方向：

```text
Presentation ──> Application ──> Repository
       │               └───────> Infrastructure
       └───────> ProjectCore ──> Application.Model / Repository

Admin.App ──> Application.Model
```

表现层负责接入与组装，业务判断应进入应用层；数据库访问进入 Repository；第三方平台和外部组件实现进入 Infrastructure

## Application

| 项目 | 职责 |
|---|---|
| `Application.Interface` | 预留需要被多个应用层或宿主共同依赖的应用抽象 |
| `Application.Model` | DTO、请求模型、返回模型和配置模型，也是管理前端复用的契约层 |
| `Application.Service` | 用户、站点、授权、支付、消息、任务中心等通用应用服务 |
| `Application.Service.LLM` | LLM 应用配置、对话管理和模型调用服务 |
| `Application.Service.SMS` | 短信发送相关应用服务 |

`Application.Interface` 不是要求所有应用服务都定义接口的传统接口层。应用服务可以直接以具体类注入，只有跨宿主或公共层确实需要共同引用的抽象才放入这里

Controller 直接通过 `ControllerBase.User` 和 WebAPI.Core 的 Claims 扩展解析身份，当前认证用户通过 `actorUserId` 或 `targetUserId` 明确传入 Application Service。应用服务不读取 HTTP 请求上下文

`Application.Service` 和 `Application.Service.LLM` 直接引用 `Application.Model`，不通过 `Application.Interface` 间接获得 DTO 项目引用

## Repository

- `Repository` 负责实体、`DatabaseContext`、`ReadDatabaseContext`、EF Core 映射、拦截器和持久化相关代码
- `Repository.Tool` 是迁移和数据库工具宿主
- 数据库结构调整应同步检查实体、上下文、映射、拦截器及调用点

## Infrastructure

Infrastructure 按能力拆分为抽象、公共实现或厂商实现，当前主要包括：

- `DistributedLock`、`DistributedLock.Redis`、`DistributedLock.InMemory`
- `FileStorage`、`FileStorage.AliCloud`、`FileStorage.TencentCloud`
- `SMS`、`SMS.AliCloud`、`SMS.TencentCloud`
- `Logger.DataBase`、`Logger.LocalFile`
- `IdentifierGenerator`
- `LLM`
- `Common`

应用层可以依赖这些能力的公共抽象，但不应把具体厂商参数或协议细节暴露给 Controller、页面或应用层 DTO

## ProjectCore

| 项目 | 职责 |
|---|---|
| `WebAPI.Core` | 认证、授权、Swagger、过滤器、健康检查、用户上下文和公共中间件 |
| `TaskService.Core` | 队列任务、定时任务、任务初始化与同步 |

ProjectCore 负责多个宿主共同需要的运行能力，不承载具体业务逻辑

## Presentation 与宿主引用

| 宿主 | 类型 | 主要应用层引用 |
|---|---|---|
| `Client.WebAPI` | 对外 Web API | `Application.Service`、`Application.Service.LLM` |
| `Admin.WebAPI` | 管理端 Web API | `Application.Service`、`Application.Service.LLM` |
| `Admin.App` | Blazor WebAssembly | `Application.Model` |
| `TaskService` | Worker Service | `Application.Service`、`Application.Service.SMS` |

`TaskService` 不引用 `Application.Service.LLM`。宿主只引用实际需要的应用类库，可以避免源码生成注册时把无关服务及其基础设施依赖带入当前宿主

## 服务注册与项目引用

仓库通过源码生成器提供以下注册入口：

- `BatchRegisterServices()` 注册当前启动项目及其引用程序集内标记了 `[RegisterService]` 的服务
- `BatchRegisterBackgroundServices()` 注册当前启动项目及其引用程序集内符合条件的后台服务

服务是否进入宿主的注册范围，取决于宿主是否引用了对应类库。因此，项目引用也是服务边界的一部分

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
| 新增业务逻辑或业务编排 | `Application.Service` 或对应的宿主特化应用项目 |
| 新增实体、查询或持久化逻辑 | `Repository` |
| 接入缓存、云平台或第三方服务 | `Infrastructure` |
| 新增 Controller 或宿主启动配置 | `Presentation` |
| 多个 WebAPI 宿主共用的 HTTP 能力 | `ProjectCore/WebAPI.Core` |
| 定时任务或队列任务公共能力 | `ProjectCore/TaskService.Core` |
| 编译期自动生成能力 | `SourceGenerator` |

修改前还应搜索同层现有实现，优先沿用当前模式，避免在表现层直接编写业务逻辑或数据库访问代码
