# 只读数据库上下文迁移检查清单

本文记录对当前仓库 EF Core 查询调用的扫描结果，用于后续把适合的查询从 `DatabaseContext` 迁移到 `ReadDatabaseContext`

本文先整理候选清单，再按风险分批实施。判断依据是当前代码行为；当 `dbReadConnection` 指向异步复制的只读副本时，必须把复制延迟纳入判断

上下文用法、只读连接配置、多读库方案和一致性边界见 [数据库读写分离](DatabaseReadWriteSeparation.md)

## 实施状态

- 已完成低风险第一步：`BaseService`、`LogManageService`、`LlmConversationManageService` 已整体迁移到 `ReadDatabaseContext`
- 尚未实施混合读写服务迁移，后续仍按本文分批处理

## 当前实现基线

- `DatabaseContext` 是读写上下文，负责实体跟踪和保存
- `ReadDatabaseContext` 继承 `DatabaseContext`，三个宿主均已使用 `QueryTrackingBehavior.NoTracking` 注册
- `Client.WebAPI`、`Admin.WebAPI` 和 `TaskService` 都已注册作用域上下文及池化工厂
- `dbReadConnection` 为空时会回退到 `dbConnection`，因此迁移查询代码后仍可在没有独立只读库的环境运行
- `ReadDatabaseContext` 只表达查询连接的使用语义，不在代码层拦截 `SaveChanges`、批量更新、批量删除或原始 SQL。生产环境的 `dbReadConnection` 必须使用数据库侧只读账号，由 PostgreSQL 权限作为最终写入边界
- 三个宿主构建 `ReadDatabaseContext` 数据源时会保留连接字符串中已有的 `Options`，并自动追加 `-c default_transaction_read_only=on`，使读取连接默认使用只读事务
- 只读上下文全局不跟踪实体，适合 DTO 投影、列表、详情和统计查询；从只读上下文取出的实体不应直接交给写上下文更新

## 判断标准

### 可以直接迁移

同时满足以下条件的查询优先迁移：

- 方法只查询，不调用 `SaveChanges`、批量更新、批量删除或写 SQL
- 查询结果用于展示、检索、报表或低频配置读取
- 短时间读到旧数据不会造成越权、重复处理、错误扣款或错误状态流转
- 方法不依赖同一请求或同一事务中刚刚写入的数据

### 需要条件迁移

存在以下情况时，只有在明确接受副本延迟，或实现“只读库未命中后回主库”的策略后再迁移：

- 创建或修改完成后，客户端通常会立即读取
- 数据控制外部调用、模型启停、任务状态或附件读取
- 管理页面对实时性有较高要求
- 查询失败或未命中会被当成业务不存在，而不是短暂延迟

### 应继续使用主库

- 查询结果用于后续写入、唯一性判断、并发控制或状态机流转
- 认证、权限判定、Token、密码与外部账号绑定
- 支付、退款、回调验签和订单状态
- 队列任务抢占、租约续期、完成和失败处理
- 初始化、同步、清理、主库写入能力检查和数据库工具流程

## 第一批：已完成的低风险迁移

这一批均为独立的纯查询方法，副本短暂延迟通常只影响页面展示，目前已经完成迁移

| 文件 | 方法 | 查询内容 | 建议改法 |
|---|---|---|---|
| `Application/Application.Service/Basic/BaseService.cs` | `GetRegionAsync` | 省、市、区字典 | 该服务没有数据库写入，可将构造参数直接改为 `ReadDatabaseContext`
| 同上 | `GetRegionAllAsync` | 完整省市区树 | 同上
| 同上 | `GetValueListAsync` | 字典配置键值对 | 同上
| `Application/Application.Service/Basic/LogManageService.cs` | `GetLogListAsync` | 日志分页与检索 | 该服务为纯查询服务，可整体改用 `ReadDatabaseContext`
| `Application/Application.Service.LLM/LlmConversationManageService.cs` | `GetLlmConversationListAsync` | LLM 调用记录分页 | 该服务为纯查询服务，可整体改用 `ReadDatabaseContext`

第一批共覆盖 5 个公开查询入口

## 第二批：满足条件后迁移

### 后台管理查询

以下查询目前由后台管理页面使用，创建、修改或删除后通常会立即刷新。为保证读取自己刚完成的写入，默认继续使用主库；只有页面明确接受最终一致，或者未来拆出独立的公共展示查询后，再迁移对应方法

| 文件 | 方法 | 风险 | 建议条件 |
|---|---|---|---|
| `Application/Application.Service/Site/SiteService.cs` | `GetSiteAsync` | 修改站点配置后立即读取可能仍显示旧值 | 当前保留主库；未来拆出允许最终一致的公共站点配置查询后再迁移 |
| `Application/Application.Service/Site/LinkService.cs` | `GetLinkListAsync`、`GetLinkAsync` | 新增、修改或删除友情链接后立即刷新可能显示旧列表或旧详情 | 后台管理查询保留主库；公共展示查询可拆成独立方法使用读库 |
| `Application/Application.Service/Site/ArticleService.cs` | 栏目和文章的列表、选择树及详情查询 | 栏目或文章保存后立即刷新可能缺项、显示旧值或残留已删除数据 | 后台管理查询保留主库；未来公共文章展示方法可以单独评估 |
| `Application/Application.Service/User/RoleService.cs` | 角色列表、详情、权限树和下拉列表 | 角色及权限调整后立即读取可能显示旧配置 | 当前保留主库；只有纯展示页面接受最终一致时再迁移 |
| `Application/Application.Service/User/UserService.cs` | 用户列表、详情、权限树和角色配置列表 | 用户、角色或权限调整后可能读取旧状态 | 当前保留主库；公共资料展示应拆分独立方法后再评估 |
| `Application/Application.Service.LLM/LlmModelService.cs` | `GetLlmModelListAsync`、`GetLlmModelSelectAsync` | 模型新建、修改或启停后列表和下拉框可能不同步 | 当前保留主库；只有管理页面接受最终一致时再迁移 |
| `Application/Application.Service.LLM/LlmAppService.cs` | `GetLlmAppListAsync` | 应用新建、修改或启停后可能显示旧状态 | 当前保留主库；只有管理页面接受最终一致时再迁移 |

### 文件读取与附件

| 文件 | 方法 | 风险 | 建议条件 |
|---|---|---|---|
| `Application/Application.Service/Basic/FileService.cs` | `GetFileUrlAsync` | 上传成功后立即获取 URL 时，副本可能尚无 `StoredFile` 记录 | 只读库未命中时回主库查询，或保证调用方能重试
| 同上 | `GetFileListAsync` | 绑定或同步文件后立即刷新列表可能缺项 | 接受短暂旧列表，或在写入后的同一请求链继续走主库
| `Presentation/Client.WebAPI/Controllers/FileController.cs` | `GetFile`、`GetImage` | 新上传文件可能短暂返回“未找到” | 先把数据库查询下沉到 `FileService`，再实现只读库查询及主库未命中回退，避免继续在 Controller 中扩展数据访问逻辑
| `Presentation/Admin.WebAPI/Controllers/FileController.cs` | `GetFile`、`GetImage` | 风险同客户端接口 | 处理方式同上
| `Application/Application.Service/MessageService.cs` | `SendEmailAsync` 中的附件元数据查询 | 队列消费可能早于副本同步，导致邮件漏附件 | 只有在未命中回主库或确认队列延迟始终大于复制延迟时才迁移

### 任务中心与运行状态

| 文件 | 方法 | 风险 | 建议条件 |
|---|---|---|---|
| `Application/Application.Service/TaskCenter/TaskSettingService.cs` | `GetTaskSettingListAsync` | 管理页面可能短暂显示旧的启停和 Cron 配置 | 若页面允许最终一致，可迁移
| 同上 | `GetArgsScheduleTaskNameListAsync` | TaskService 刚同步的新任务名称可能暂时不可见 | 可接受稍后重试时再迁移
| `Application/Application.Service/TaskCenter/QueueTaskManageService.cs` | `GetQueueTaskListAsync` | 运行中任务的状态、次数、租约时间变化频繁 | 仅作为历史/概览页面时迁移；实时运维页面保留主库
| `Presentation/TaskService/Tasks/DemoTask.cs` | `ShowTime` 中的首个用户查询 | 仅为演示查询，业务价值较低 | 可以迁移，但优先级最低

`RetryQueueTaskAsync`、`UpdateTaskSettingAsync`、`CreateScheduleTaskAsync` 仍必须完整使用主库，不能因为同一个服务新增了 `readDb` 而误换

### LLM 运行配置

| 文件 | 方法 | 风险 | 建议条件 |
|---|---|---|---|
| `Application/Application.Service.LLM/LlmModelConfigResolver.cs` | `GetConfigAsync` | 模型禁用、密钥轮换或端点修改在复制延迟期间不会立即生效 | 明确允许配置最终一致，或增加短 TTL 缓存并提供主动失效机制后再迁移
| `Application/Application.Service.LLM/LlmInvokeService.cs` | `ChatAsync`、`ChatStreamAsync` 中的 LLM 应用查询 | 已禁用应用可能在短时间内继续产生外部调用和费用 | 默认保留主库；只有业务接受启停延迟时才拆分为读库查询，调用记录仍写主库
| `Application/Application.Service.LLM/LlmAppService.cs` | `TestLlmAppAsync` 调用链中的模型启用校验 | 刚禁用的模型可能仍被测试调用 | 默认保留主库

`LlmInvokeService` 同时读取配置并写入 `LlmConversation`。若未来迁移其配置查询，必须同时注入两个上下文，不能把对话记录写入 `ReadDatabaseContext`

### 授权菜单展示

| 文件 | 方法 | 风险 | 建议条件 |
|---|---|---|---|
| `Application/Application.Service/AuthorizeService.cs` | `GetFunctionListAsync` | 权限收回后菜单可能短暂仍可见 | 仅当真正的接口授权仍由主库校验时可迁移；它只能影响菜单展示，不能作为安全边界

## 明确保留主库的调用

以下调用即使表面上包含只读查询，也与写入、并发或安全决策绑定，不建议拆到只读副本

### 写命令及其前置查询

- `SiteService` 的 `EditSiteAsync`、`SetSiteInfoAsync`
- `LinkService`、`ArticleService`、`RoleService`、`UserService` 的所有创建、更新、删除和授权设置方法
- `FileService` 的上传、绑定、内容文件同步、业务文件软删除和单文件删除方法
- `TaskSettingService` 的更新与创建方法
- `QueueTaskManageService.RetryQueueTaskAsync`
- `LlmModelService`、`LlmAppService` 的创建、更新与删除方法
- `LlmInvokeService.TrySaveConversationAsync`

这些方法中的实体查询依赖跟踪，唯一性/存在性判断需要看到主库最新状态，后续写入也应留在同一个上下文

### 认证、授权与账号安全

- `AuthorizeService` 的登录、短信登录、微信登录、密码修改、Token 签发和刷新
- `AuthorizeService.CheckFunctionAuthorizeAsync`
- `AuthorizeService` 读取微信 AppSecret 的两个辅助方法
- `Presentation/Admin.WebAPI/Controllers/AuthorizeController.cs` 的路由同步与初始化数据流程
- `Presentation/Client.WebAPI/Controllers/AuthorizeController.cs` 的路由同步流程

权限撤销、密码修改和 Token 状态必须尽快生效。副本延迟可能造成已撤销权限继续生效、重复创建绑定用户或读取旧密钥，因此不应迁移

### 支付与订单

`Application/Application.Service/PayService.cs` 整体保留主库，包括支付发起阶段的订单和商户配置查询。虽然部分方法当前只读取数据并调用第三方接口，但旧订单状态、旧密钥或旧金额都可能产生资金风险，不适合用普通异步只读副本

### 队列、定时任务与后台写入

- `Application/Application.Service/TaskCenter/QueueTaskService.cs`
- `ProjectCore/TaskService.Core/QueueTask/QueueTaskService.cs`
- `ProjectCore/TaskService.Core/QueueTask/QueueTaskBackgroundService.cs`
- `ProjectCore/TaskService.Core/TaskSettingSyncBackgroundService.cs`
- `ProjectCore/TaskService.Core/InitTaskBackgroundService.cs`
- `Infrastructure/Logger.DataBase/Tasks/LogSaveTask.cs`
- `Infrastructure/Logger.DataBase/Tasks/LogClearTask.cs`

特别是 `QueueTaskBackgroundService.GetCandidateTaskIdsAsync` 虽然是纯查询，但它紧接着执行任务抢占。若从副本读取，会漏掉新任务，也可能基于旧租约状态重复参与抢占，所以应与整个队列状态机一起留在主库

`InitTaskBackgroundService` 也应保留主库，因为启动同步可能刚向主库插入 `TaskSetting`，立刻从副本读取会漏掉新配置

### 宿主、健康检查和数据库工具

- `Repository/DatabaseHealthCheck.cs` 继续检查主库，确保宿主具备实际写入所需的数据库可用性
- `Repository/ReadDatabaseHealthCheck.cs` 独立检查数据库读取连接，不替换主库健康检查
- `Repository.Tool/Tasks/SyncJsonIndexTask.cs` 面向主数据库结构，不迁移
- 三个宿主 `Program.cs` 只负责注册，无业务查询需要迁移

## 推荐实施顺序

1. 已完成 `BaseService`、`LogManageService` 和 `LlmConversationManageService` 这三个纯查询服务的迁移
2. 后台管理查询默认继续使用主库；未来出现允许最终一致的公共展示用例时，优先拆分独立查询方法，再注入 `ReadDatabaseContext readDb`
3. 在真实只读连接上验证软删除过滤器、JSON 列映射、连接权限和连接池配置
4. 增加只读库健康状态、查询失败率和 PostgreSQL 复制延迟监控
5. 根据可接受的最大复制延迟，再决定是否实施文件、任务管理和 LLM 运行配置等第二批项目

建议在混合服务中明确使用 `writeDb` 与 `readDb` 命名，避免后续维护时把写操作误放到只读上下文。不要为了切换上下文新增通用 Repository 包装层，沿用当前服务直接使用 EF Core 的模式即可

## 每次迁移后的验证项

- `dbReadConnection` 为空时，三个宿主仍能正常启动和查询
- `dbReadConnection` 指向只读账号时，目标查询成功，任何误写操作被数据库拒绝
- 软删除实体不会重新出现在读库结果中
- `LlmApp`、`LlmModel` 等 JSON 列投影和反序列化行为与主库一致
- 分页接口的 `Total` 和 `List` 来自同一个只读上下文；接受两条 SQL 之间数据变化造成的轻微差异
- 新增、更新或删除后立即读取的页面，对副本延迟有明确表现或回退策略
- 读库不可用时的行为符合预期：直接失败、熔断或回主库三者需要明确选择，不能默认假设自动回退
- 分别启动并验证 `Client.WebAPI`、`Admin.WebAPI` 和 `TaskService` 中实际涉及的服务
- 最后执行一次 `dotnet build NetEngine.slnx`
