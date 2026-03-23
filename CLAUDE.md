# AGENTS.md

## 1. 文档目的

本规范用于约束 AI Agent 在 `NetEngine` 仓库中的分析、修改、验证与协作方式

本仓库是一个基于 .NET 10 的分层解决方案，核心能力包括 Web API、Blazor WASM 管理端、任务调度、EF Core、基础设施组件与源码生成器

Agent 在本仓库中工作时，必须遵守以下原则

- 保持现有分层结构稳定
- 优先复用仓库中已存在的实现模式
- 优先做小范围、可验证、可维护的修改
- 代码风格尽量贴近当前仓库以及微软官方 ASP.NET Core 与 EF Core 常见写法

## 2. 解决方案结构

### 2.1 Application

- `Application.Interface`
  - 应用层接口定义
- `Application.Model`
  - DTO、请求模型、返回模型
- `Application.Service`
  - 业务编排与业务逻辑实现

### 2.2 Repository

- `Repository`
  - EF Core 实体、`DatabaseContext`、拦截器、数据库访问逻辑
- `Repository.Tool`
  - EF Core 迁移与数据库工具宿主

### 2.3 Infrastructure

- 各类基础设施实现
- 包括缓存、分布式锁、日志、短信、文件存储、LLM、ID 生成等能力

### 2.4 ProjectCore

- 公共宿主能力
- 包括 `WebAPI.Core` 与 `TaskService.Core`

### 2.5 Presentation

- 宿主与表现层项目
- 包括 `Client.WebAPI`、`Admin.WebAPI`、`Admin.App`、`TaskService`

### 2.6 SourceGenerator

- 编译期源码生成器与其运行时支持代码

### 2.7 InitData

- 初始化数据文件

## 3. 分层与职责规范

### 3.1 总体原则

- 依赖方向必须保持稳定
- 表现层负责宿主组装、请求接入、参数传递与结果返回
- 应用层负责业务逻辑、业务编排与用例实现
- 数据访问层负责 EF Core、实体模型、数据库相关逻辑
- 基础设施层负责第三方组件和外部服务接入

### 3.2 Presentation 层约束

- 业务逻辑不允许写在 `Presentation` 层
- 包括各个 API 项目与 `TaskService` 项目
- `Presentation` 层应尽可能只保留对 `Application` 层的调用
- `Presentation` 层可以保留宿主启动、依赖注入、请求接收、鉴权、中间件、参数适配、返回包装等表现层职责
- 不允许在 Controller、Razor 页面、宿主启动代码中堆放核心业务判断
- 不允许把数据库访问逻辑直接写入 `Presentation` 层

### 3.3 Application 层约束

- 业务逻辑优先放在 `Application.Service`
- 新功能优先扩展现有 Service、DTO、接口，而不是平行新增一套风格不同的实现
- 不要把宿主启动相关逻辑、HTTP 细节、页面细节混入应用层
- 如果某个应用服务所在的应用类库会被多个宿主共同引用，并导致部分宿主为了启动不报错而被迫注册本宿主根本不用的基础设施能力，应优先将该宿主特化服务拆分到独立应用类库
- 遇到上述情况时，优先参考 `Application.Service.LLM` 与 `Application.Service.SMS` 的拆分方式，而不是继续使用可空依赖、空实现或让所有宿主补齐注册

### 3.4 Repository 层约束

- `Repository` 负责实体、`DatabaseContext`、EF Core 映射、拦截器与持久化相关代码
- 数据库结构变更时，应优先修改 `Repository` 中的实体与相关配置
- 不要在无关层中写数据库方言相关代码

### 3.5 Infrastructure 层约束

- 第三方平台、云服务、缓存、锁、日志、文件、短信、LLM 等能力应放在 `Infrastructure`
- 不要把基础设施实现细节泄漏到 DTO、Controller 或页面代码中

## 4. Source Generator 规范

本仓库通过 `Directory.Build.props` 自动接入源码生成器

- 服务注册优先通过 `BatchRegisterServices()`
- 后台服务注册优先通过 `BatchRegisterBackgroundServices()`
- 代理拦截能力由 `SourceGenerator.Runtime` 支持
- 软删除过滤器与 JSON 列映射优先使用生成代码能力

涉及注册、拦截、代理相关代码时，优先遵守以下规则

- 先检查是否已有基于特性的生成式做法
- 能复用生成器时，不手写重复的 DI 注册代码
- 需要代理拦截的方法，保持与现有模式一致，通常应保留 `virtual`
- 不要破坏启动项目现有的生成注册链路

## 5. 代码风格规范

### 5.1 通用要求

- 大多数项目目标框架为 `net10.0`
- 部分共享项目或运行时项目同时使用 `net10.0-browser`
- 全仓库启用了 Nullable 与 ImplicitUsings
- 编写代码时必须考虑空引用安全
- 代码风格优先服从当前目录下已有代码
- 优先选择直接、清晰、低包装的实现方式
- 无明确必要时，不引入新的抽象层

### 5.2 注释要求

- Agent 新增或修改的 `class` 应补充中文注释
- Agent 新增或修改的方法应补充中文注释
- Agent 新增或修改的属性应补充中文注释
- 注释结尾不要使用句号、顿号、分号、冒号等标点
- 注释内容应直接说明职责、用途或语义，不写空话

### 5.3 排版要求

- 方法参数必须写在同一行
- 即使参数有多个，也不要主动换行
- 方法与方法之间保留两个空行
- 类中的属性与属性之间保留两个空行
- 类体内部开头与结尾相对外层大括号各保留一个空行
- 方法体内部开头与结尾相对外层大括号各保留一个空行
- 如用户未明确要求，不主动采用与当前规范冲突的自动格式化风格

### 5.4 注释与字符要求

- 注释优先使用中文
- 新建和修改文件默认使用 UTF-8 without BOM 字符编码
- 如无特殊原因，不使用 GBK、ANSI 等其他本地编码
- 如果文件本身已包含中文内容，或业务语义必须使用中文，可继续保持中文

## 6. Web 与宿主规范

- Web 宿主启动代码通常位于 `Presentation/*/Program.cs`
- 公共中间件与宿主扩展能力通常位于 `ProjectCore/WebAPI.Core`
- 健康检查路径为 `/healthz`
- Swagger 默认按开发环境使用理解，除非现有代码明确说明其他行为

修改 API 行为时，至少同时检查以下位置

- 对应的 Controller
- 对应的 Application Service
- 对应的 DTO 或请求模型

## 7. 数据库与 EF Core 规范

- 默认数据库提供程序为 PostgreSQL
- `DatabaseContext` 与相关拦截器位于 `Repository`
- 涉及数据库结构变更时，应同步考虑实体、上下文、映射、拦截器及相关调用点
- 如任务涉及 EF Core 结构调整，Agent 只负责代码修改
- 除非用户明确提出，否则不要帮用户生成 EF Migration
- 除非用户明确提出，否则不要帮用户执行 EF Migration
- 涉及迁移相关问题时，优先检查 `Repository.Tool`

## 8. 配置规范

- 新增配置项前，先检查对应宿主下的 `appsettings.json` 与 `appsettings.Development.json`
- 优先复用现有配置节名称、绑定方式与 Options 模式
- 不提交真实密钥、真实连接串或真实密码
- 仓库中的密钥类配置默认视为示例值或本地开发值

## 9. TaskService 规范

- 任务宿主主要位于 `Presentation/TaskService`
- 任务基础能力位于 `ProjectCore/TaskService.Core`
- 队列任务与定时任务应优先复用现有 Builder、Attribute 与注册方式
- Debug 模式下 `TaskService` 存在交互式启用流程
- 修改任务宿主时，不要轻易破坏该调试行为

## 10. 前端规范

- `Presentation/Admin.App` 为 Blazor WebAssembly 项目
- `Presentation/Admin.WebAPI` 负责管理端 API 与静态资源相关宿主职责
- 修改 `.razor` 页面时，优先保持当前项目现有组件风格与结构
- 如无明确要求，不做大规模界面风格重写

## 11. 工作方式规范

### 11.1 修改前

- 先阅读将要修改的文件及其相邻调用链
- 至少向上或向下多看一层
- 先搜索仓库内是否已有相同问题的现成实现
- 优先确认是否已有生成器、扩展方法或共享基础能力可复用

### 11.2 修改时

- 修改范围尽量收敛
- 不做与当前任务无关的大型重构
- 跨层功能变更时，应同步处理 Service、DTO、Controller、配置等相关内容
- 优先沿用当前层已经存在的实现模式
- 如果问题本质是宿主引用范围过大导致无关基础设施依赖外溢，优先通过拆分宿主特化应用类库来收敛注册范围

### 11.3 修改后

- 先做最小必要验证
- 如果改动影响多个项目或启动组合，再考虑解决方案级别构建验证
- 除非用户明确要求，否则默认不主动编写单元测试代码
- 不要并行执行多个 `dotnet build`，应串行构建，避免 `SourceGenerator.Core.dll` 被 `VBCSCompiler` 占用导致构建失败

## 12. 验证命令

以下命令可在仓库根目录按需执行

```powershell
dotnet restore
dotnet build NetEngine.slnx
dotnet run --project Presentation/Client.WebAPI/Client.WebAPI.csproj
dotnet run --project Presentation/Admin.WebAPI/Admin.WebAPI.csproj
dotnet run --project Presentation/Admin.App/Admin.App.csproj
dotnet run --project Presentation/TaskService/TaskService.csproj
```

## 13. 决策优先级

面对多个实现方案时，按以下顺序决策

1. 复用同层中已经存在的实现模式
2. 复用 `ProjectCore`、`Infrastructure` 或源码生成器提供的公共能力
3. 添加最小必要代码
4. 最后才考虑引入新的抽象

## 14. 明确禁止事项

- 不要新增无必要的包装层
- 不要绕过现有生成式 DI 注册链路，除非确有必要
- 不要把业务逻辑写进 Controller、Razor 页面、API 宿主或任务宿主
- 不要把数据库访问直接塞进表现层
- 不要把基础设施实现细节混入应用层模型或表现层代码
- 不要进行与任务无关的大规模风格化重写
