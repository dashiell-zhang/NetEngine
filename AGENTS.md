# AGENTS.md

## 1. 适用范围与执行原则

本文件约束 Agent 在 `NetEngine` 仓库中的分析、修改、验证与交付，适用于整个仓库

本仓库以 .NET 10 为主，包含 Web API、Blazor WebAssembly 管理端、任务调度、EF Core、基础设施组件与源码生成器

- 在系统与开发者指令允许的范围内，用户当前任务中的明确要求优先于本文件和技能中的默认约定
- 修改前检查目标目录及其父目录中适用的 `AGENTS.md` 与 `AGENTS.override.md`，同一目录优先读取 `AGENTS.override.md`，更深目录的规则只对其作用范围生效
- 本文件中的“必须”“不要”“禁止”为约束，“默认”“优先”“按需”为允许结合上下文判断的原则，不要把偏好自行升级为审批要求
- 代码决定当前运行行为，文档说明预期约定；发现不一致时先核对实现与用户目标，不要把现存缺陷自动视为必须保留的规范
- 保持分层和依赖方向稳定，优先完成小范围、可验证、可维护的修改
- 避免过度设计，以满足当前明确需求的最简单清晰实现为默认选择，新增复杂度必须有具体收益

在职责、业务语义和依赖方向匹配的前提下，按以下顺序选择实现：同层已有模式 → 现有公共能力或生成器 → 最小必要代码 → 有明确收益的新抽象，不为复用引入更多耦合或绕行

## 2. 工作流程与完成标准

### 2.1 修改前

- 根据用户请求判断任务属于分析、审查还是修改；明确的修改请求应直接落实，纯分析或审查请求默认不改文件
- 先执行 `git status --short`，识别已有修改、删除和未跟踪文件；需要修改同一文件时先查看相关差异
- 使用 `rg` 或 `rg --files` 定位实现，优先搜索相关目录；工具不可用时再使用替代方式
- 按第 3 节阅读与任务相关的文档，再阅读目标文件及至少一层调用方或被调用方；文档和配置修改检查相关引用或使用入口，不要求每次通读全部专题文档，已读且未变化的内容无需重复读取
- 搜索仓库内同类实现，确认可复用的 Service、DTO、生成器、扩展方法与公共能力
- 修改前明确预期行为、影响范围与必要验证；简单任务直接执行，跨层或多步骤任务使用简短计划

### 2.2 修改与协作

- 对任务范围内的读取、局部编辑、修复与必要验证持续推进，不停留在建议或计划，不反复询问已获授权的步骤
- 常规实现细节按现有模式作合理选择；先从当前上下文、文档和代码中核实信息，仍无法确定且会实质改变业务行为、公共契约、数据处理或授权范围时才询问，并继续不依赖答案的工作
- 用户补充要求时，将其纳入当前任务；除非用户明确取消或替换目标，否则保留已完成工作与原有目标
- 修改范围应覆盖实现目标所需的调用链；“小范围”不意味着遗漏必要的 Service、DTO、Controller、配置或文档调整
- 不做无关重构、批量格式化、依赖升级或项目拆分，不为假设中的未来需求新增包装层
- 保留用户和其他任务的已有修改，不擅自恢复删除文件、覆盖改动或清理未跟踪文件；若与必要修改直接冲突且无法可靠合并，再说明冲突并询问
- 独立的只读搜索可以并行；存在依赖关系的编辑、生成和验证按顺序执行，共享构建产物的进程遵守第 7 节
- 若规则或技能确实阻止执行，说明具体文件、条款及受阻步骤，先完成其他已获授权的工作

### 2.3 完成与交付

- 交付前检查最终差异，确认需求已落实、相关调用点一致，本次修改未遗留不需要的临时文件或引入无关改动，不清理任务开始前已有的内容
- 本次变更影响稳定用法或发现相关文档已失效时，同步修正对应文档，避免保留与最终行为矛盾的说明
- 执行与改动匹配的验证；失败时先判断属于本次修改、已有问题还是环境限制，修复本次引入的问题
- 不通过删除校验、吞掉异常、关闭 Nullable 或扩大警告抑制范围来掩盖失败
- 已有证据足以支持结论后停止重复验证；只有新修改、新失败或未解决风险才扩大验证范围
- 默认使用中文，简要说明改了什么、验证结果以及仍未完成的事项；引用具体文件，避免粘贴完整日志
- 对本次改动必要但未执行的构建、测试或运行检查，说明原因与验证缺口；不逐项罗列不适用的检查，构建通过不等于运行行为已经验证

## 3. 文档入口与仓库导航

`AGENTS.md` 保存稳定的工作约束，根目录 [README.md](README.md) 提供项目简介和启动入口，[Docs](Docs/README.md) 保存能力用法，具体实现以代码为依据

| 修改范围 | 必读文档 |
|---|---|
| 分层、依赖方向或项目拆分 | [Architecture.md](Docs/Architecture.md) |
| WebAPI 宿主、公共配置、中间件、认证或健康检查 | [WebAPI.md](Docs/WebAPI.md) |
| WebAPI 过滤器 | [WebAPIFilters.md](Docs/WebAPIFilters.md) |
| 服务注册、代理或 EF Core 生成能力 | [SourceGenerator.md](Docs/SourceGenerator.md) |
| 数据库上下文、读写分离或读库配置 | [DatabaseReadWriteSeparation.md](Docs/DatabaseReadWriteSeparation.md) |
| PostgreSQL 分区表、分区声明或维护 | [PostgreSqlPartitionTable.md](Docs/PostgreSqlPartitionTable.md) |
| 定时任务或队列任务 | [TaskService.md](Docs/TaskService.md) |
| 分布式锁或并发信号量 | [DistributedLock.md](Docs/DistributedLock.md) |
| LLM 应用配置、调用或 Provider 扩展 | [LLM.md](Docs/LLM.md) |
| 部署配置、模板或生成器 | [Deployment.md](Docs/Deployment.md) |

| 目录或项目 | 职责 |
|---|---|
| `Application/Application.Interface` | 跨宿主、跨公共层共享的应用抽象，不要求所有应用服务定义接口 |
| `Application/Application.Model` | DTO、请求模型、返回模型与配置模型 |
| `Application/Application.Service` | 通用业务逻辑与业务编排 |
| `Application/Application.Service.LLM` | LLM 应用服务 |
| `Application/Application.Service.SMS` | 短信应用服务 |
| `Repository` | EF Core 实体、上下文、映射、拦截器与持久化逻辑 |
| `Repository.Tool` | EF Core 迁移与数据库工具宿主 |
| `Infrastructure` | 缓存、锁、日志、短信、文件存储、LLM、ID 生成等基础设施 |
| `ProjectCore` | `WebAPI.Core` 与 `TaskService.Core` 公共宿主能力 |
| `Presentation` | `Client.WebAPI`、`Admin.WebAPI`、`Admin.App` 与 `TaskService` 宿主 |
| `SourceGenerator` | 编译期源码生成器及生成代码运行时支持 |
| `InitData` | 初始化数据文件 |
| `Deployment` | 部署配置生成器、模板、输入配置与生成产物 |

## 4. 分层与实现约束

### 4.1 Presentation 与 ProjectCore

- `Presentation` 负责宿主组装、请求接入、鉴权、参数适配、页面交互与结果返回，业务逻辑通过 Application 调用
- 禁止在 Controller、Razor 页面、API 或任务宿主中编写核心业务判断和数据库访问逻辑
- 宿主可以注册数据库上下文和基础设施服务，依赖注入组装不等于业务数据访问
- `ProjectCore` 承载多个宿主共用的启动和运行能力，不承载具体业务逻辑
- `Admin.App` 为 Blazor WebAssembly 项目，复用 `Application.Model` 契约；保持现有组件风格与结构，不主动大规模重写界面
- `Admin.WebAPI` 承担管理端 API 与静态资源相关宿主职责

### 4.2 Application

- 通用业务放入 `Application.Service`，领域特化业务放入对应应用类库，优先扩展现有 Service、DTO 与必要接口
- 应用服务不读取 HTTP 请求上下文，不混入宿主启动或页面细节；身份等参数由表现层明确传入
- 若通用应用类库使部分宿主被迫注册不用的基础设施，优先参考 `Application.Service.LLM` 和 `Application.Service.SMS` 拆分宿主特化应用类库
- 不使用可空依赖、空实现或让所有宿主补齐无关注册来掩盖上述引用边界问题，也不单纯为目录整齐拆分项目

### 4.3 Repository 与 Infrastructure

- 实体、数据库上下文、EF Core 映射、拦截器与持久化能力放在 `Repository`，不要在无关层编写数据库方言代码
- 第三方平台与外部服务接入放在 `Infrastructure`，优先沿用同类能力已有的抽象与厂商实现组织方式
- 不把具体厂商参数、协议或基础设施实现细节泄漏到应用层 DTO、Controller 或页面

### 4.4 源码生成器与依赖注入

- 自动引用规则位于 `Directory.Build.props`，生成文件输出路径位于 `Directory.Build.targets`
- 普通业务项目隐式引用 `SourceGenerator.Core` 与 `SourceGenerator.Runtime`；`Infrastructure`、`Deployment` 和生成器自身不参与这套隐式引用
- 服务注册优先使用特性与 `BatchRegisterServices()`，后台服务注册优先使用 `BatchRegisterBackgroundServices()`，不重复手写已覆盖的注册或项目引用
- 基础设施和框架组件继续使用已有扩展注册方式，不为统一形式强行套用业务服务生成规则
- 代理拦截由 `SourceGenerator.Runtime` 支持；普通类中需要拦截的方法通常保留 `public virtual`，通过 DI 获取服务，接口代理的具体规则见专题文档
- 软删除过滤器、JSON 列映射与分区表声明优先复用生成能力
- 不直接修改编译生成文件；修改生成器输入、生成逻辑或运行时支持，并检查消费项目的生成结果
- 修改项目引用或注册范围时，检查受影响宿主，确保没有引入无关服务依赖或破坏生成注册链路

### 4.5 避免过度设计

- 只实现当前明确需要的行为，不为“以后可能用到”预留扩展点、配置开关、通用框架或兼容分支
- 能在现有职责内用直接调用、清晰条件判断或少量方法完成的功能，优先保持这种写法，不为套用设计模式引入多层转发
- 新增接口、基类、工厂、策略或独立项目，应能说明它解决了当前哪项复用、变化隔离或依赖边界问题；仅有一个实现且没有这些需求时，优先使用具体类
- 抽取公共能力前确认业务语义一致，少量形式相似但变化原因不同的代码可以保留，不为消除几行重复引入复杂泛型、继承层次或大量控制参数
- 方法和类按职责与可读性拆分，不机械地把每几行代码拆成一个方法，也不以减少文件或行数为由堆积不同职责
- 不在没有明确性能要求或瓶颈证据时引入缓存、并行处理等性能优化机制；保证幂等、并发安全或数据一致性所必需的措施按业务要求实现，优先复用仓库已有能力
- 简化设计仍须保留必要的参数校验、鉴权、错误处理、数据一致性与分层边界；需要抽象或拆分时，采用能解决当前问题的最小方案

## 5. 专项变更规则

### 5.1 Web API

- 启动代码通常位于 `Presentation/*/Program.cs`，公共宿主扩展位于 `ProjectCore/WebAPI.Core`
- 修改 API 行为时，检查 Controller → Application Service → DTO 或请求模型；契约发生变化时继续检查相关前端与其他调用方
- 保持现有鉴权、参数校验、异常处理与返回包装模式
- 用户未要求改变的接口契约保持兼容，包括路由、字段、状态码和序列化行为；检查已有调用方，不以简化设计为由删除仍在使用的兼容逻辑
- 健康检查路径为 `/healthz`；Swagger 的启用条件以现有环境判断为准，默认按开发环境使用理解

### 5.2 数据库与 EF Core

- 默认提供程序为 PostgreSQL，读写上下文和相关拦截器位于 `Repository`
- 结构调整需同步检查实体、上下文、映射、拦截器、生成能力及调用点，读写分离调整需核对读库配置和查询路径
- 默认只修改代码；未经用户明确要求，不生成、不执行 EF Migration，不以验证为由更新数据库结构
- 用户要求生成 Migration 不等于授权执行 Migration，迁移工具入口优先检查 `Repository.Tool`

### 5.3 配置

- 新增配置项前检查对应宿主的 `appsettings.json` 与 `appsettings.Development.json`，复用现有节名称、绑定方式与 Options 模式
- 未经用户明确授权，不把真实密钥、连接串或密码写入仓库；新增示例使用明确的占位值
- 不因配置位于仓库中就认定它不敏感，不在日志、文档或交付说明中复制凭据
- 不为让本地验证通过而擅自替换用户连接配置或改变默认业务行为

### 5.4 TaskService

- 任务宿主位于 `Presentation/TaskService`，公共能力位于 `ProjectCore/TaskService.Core`
- 任务声明、入队、调度、回调与重试复用现有 Builder、Attribute 和注册方式
- 保留 Debug 模式下的交互式启用流程，验证时考虑控制台输入与后台任务启动行为

### 5.5 LLM

- 基础设施位于 `Infrastructure/LLM`，应用能力位于 `Application/Application.Service.LLM`
- 业务代码优先通过 `LlmInvokeService` 调用，不直接依赖具体 Provider 客户端
- 宿主只在实际使用 LLM 时引用对应应用类库，配置和 Provider 扩展方式见专题文档

### 5.6 部署生成器

- 修改 `Deployment` 中的配置、模板或生成逻辑，不直接编辑 `Deployment/Generated`
- 生成器成功运行后会完整替换 `Generated`，包括删除额外文件；运行前检查该目录是否有用户改动或手工文件，存在覆盖风险时先保护这些内容
- 本地生成部署文件与实际部署是不同操作，生成请求不自动授权连接服务器、安装服务或执行部署命令

## 6. 代码风格与文件格式

### 6.1 通用要求

- 目标框架以项目文件为准：多数项目为 `net10.0`，浏览器相关项目包含 `net10.0-browser`，`SourceGenerator.Core` 为 `netstandard2.0`
- 项目启用 Nullable，业务项目通常启用 ImplicitUsings；保持空引用安全，不假设所有项目具有相同的隐式命名空间或 API 可用范围
- 优先选择直接、清晰、低包装的实现，本节明确规则优先于历史文件中的偶然风格，未规定部分沿用当前目录写法

### 6.2 中文注释

- 新增或修改的 `class`、方法和属性应有中文注释，直接说明职责、用途或语义
- 修改已有成员时同步更新失效注释；已有准确注释可以保留，不因局部修改而补写整个文件中无关成员的注释
- 注释结尾不使用句号、顿号、分号、冒号等标点，注释形式沿用相邻代码的 XML 文档注释或普通注释风格

### 6.3 排版与编码

- 方法声明的参数写在同一行，即使参数较多也不主动换行
- 方法与方法之间、类中的属性与属性之间保留两个空行
- 使用块体的类和方法，在起始大括号之后及结束大括号之前各保留一个空行；不为满足空行规则把现有表达式体或自动属性改成块体
- 不主动应用与上述规范冲突的自动格式化，不为局部修改重排整个文件
- 新建和修改文件默认使用 UTF-8 without BOM，保留中文内容，避免引入 GBK、ANSI、乱码或无关换行差异

## 7. 验证策略与命令

按改动选择最小但充分的验证，不把以下命令当作每次必须全部执行的清单；下表按实际行为影响选择，纯注释或排版修改不因文件属于某个项目而自动触发构建

| 改动类型 | 必要验证 |
|---|---|
| 仅 Markdown | 检查相对链接、代码块配对、UTF-8 without BOM，执行 `git diff --check`，不要求构建 |
| 仅代码注释或排版 | 确认没有改变语法、编译指令或运行语义，执行 `git diff --check`，通常不要求构建 |
| 单个项目内的代码 | 构建受影响项目，并按行为变化执行已有相关测试或针对性检查 |
| 公共契约、源码生成器、项目引用或多宿主链路 | 构建覆盖受影响链路的消费项目或宿主；影响范围广或难以界定时执行解决方案构建，不因改动涉及两个项目就自动全量构建 |
| Razor 页面或前端交互 | 构建 `Admin.App`；影响视觉或交互时，在可用环境中检查对应页面 |
| 配置文件 | 检查语法、配置键及对应绑定和读取逻辑；构建不能替代配置校验，影响启动行为时继续执行宿主验证 |
| 宿主启动或外部依赖 | 构建后按需启动相关宿主，检查启动日志与适用的 `/healthz`；健康检查通过不能替代本次改动涉及的接口或业务行为验证，缺少依赖时明确未验证部分 |
| 部署模板或生成逻辑 | 构建 `Deployment`，在保护已有产物后运行生成器并检查产物差异，不自动执行部署 |

- 除非用户明确要求，默认不新增单元测试代码；已有相关测试应按需运行
- 不同时启动多个独立的 `dotnet build` 进程，单个构建内的 MSBuild 并行可保持默认；消费项目构建已覆盖所需的引用项目时，不再逐个重复构建
- `dotnet run`、`dotnet test`、`dotnet publish` 也可能隐式构建，同样避免与其他构建过程争用共享输出
- 仅在对应产物已经按相同配置成功构建后使用 `--no-build`，不要依赖陈旧产物验证新修改
- `dotnet build` 默认执行还原，只有需要单独诊断还原或准备依赖时才先执行 `dotnet restore`
- 构建占用、缺少 SDK、workload、网络或外部服务导致失败时先定位原因，不盲目重复执行或终止其他任务的进程
- 宿主运行可能连接数据库、Redis 并启动后台任务；先核对启动路径与目标环境，只在本次授权范围内进行运行验证，结束后关闭本次启动且不再需要的进程

在仓库根目录按需执行：

```powershell
# 检查当前修改与空白问题
git status --short
git diff --check

# 单项目构建示例，替换为实际受影响项目
dotnet build Application/Application.Service/Application.Service.csproj

# 跨项目验证
dotnet build NetEngine.slnx

# 仅在需要单独还原时执行
dotnet restore
```

需要运行验证时，选择对应宿主，不要一次启动全部项目：

```powershell
dotnet run --project Presentation/Client.WebAPI/Client.WebAPI.csproj
dotnet run --project Presentation/Admin.WebAPI/Admin.WebAPI.csproj
dotnet run --project Presentation/Admin.App/Admin.App.csproj
dotnet run --project Presentation/TaskService/TaskService.csproj
```
