# NetEngine 使用文档

这里集中维护仓库内公共能力和工具的详细用法。根目录 `README.md` 主要介绍项目结构、快速启动和文档入口，具体使用约定以本目录文档及实际代码为准

## 文档导航

| 文档 | 适合在什么时候阅读 | 主要内容 |
|---|---|---|
| [架构与项目边界](Architecture.md) | 新增功能、调整分层或拆分项目时 | 分层职责、依赖方向、宿主引用与项目拆分原则 |
| [WebAPI 公共能力](WebAPI.md) | 调整 API 宿主、公共配置或中间件时 | 启动链路、配置、CORS、认证、Swagger 和健康检查 |
| [部署配置生成器](Deployment.md) | 需要生成 Nginx、systemd 或云效流水线配置时 | 配置项、生成命令、产物和首次部署准备 |
| [分布式锁](DistributedLock.md) | 需要防止重复执行或限制跨实例并发时 | Redis 锁、内存锁、等待与立即返回、租约续期 |
| [源码生成器](SourceGenerator.md) | 新增服务、后台服务、代理行为或 EF Core 映射时 | 自动 DI、AutoProxy、软删除过滤器、JSON 列映射和分区声明 |
| [数据库读写分离](DatabaseReadWriteSeparation.md) | 迁移查询、配置读库或规划多个读副本时 | 读写上下文、连接配置、一致性边界和健康检查 |
| [PostgreSQL 分区表](PostgreSqlPartitionTable.md) | 为新实体声明雪花 ID 分区或维护后续子分区时 | 实体注解、UTC+8 周期单位、Migration SQL 和 Repository 自动维护 |
| [TaskService](TaskService.md) | 新增定时任务或队列任务时 | 任务声明、入队、启用、调度、回调、子任务和重试 |
| [WebAPI 过滤器](WebAPIFilters.md) | 为 Controller 或 Action 增加通用 HTTP 行为时 | 异常、缓存、ETag、并发限制、RSA 解密和签名校验 |
| [LLM 调用](LLM.md) | 使用模型生成文本或扩展 LLM 协议时 | 模型与应用配置、提示词模板、普通和流式调用 |

## 建议阅读顺序

新人第一次接触仓库时，建议先阅读根目录 [README](../README.md) 完成本地启动，再阅读 [架构与项目边界](Architecture.md) 和 [源码生成器](SourceGenerator.md)，了解代码放置位置与自动注册方式

按任务类型继续阅读：

- 修改并发控制、缓存防击穿或任务执行锁：阅读 [分布式锁](DistributedLock.md)
- 调整 API 宿主、公共配置、CORS 或认证：阅读 [WebAPI 公共能力](WebAPI.md)
- 迁移数据库查询或配置只读副本：阅读 [数据库读写分离](DatabaseReadWriteSeparation.md)
- 为新表启用雪花 ID 分区：阅读 [PostgreSQL 分区表](PostgreSqlPartitionTable.md)
- 新增任务宿主能力：阅读 [TaskService](TaskService.md)
- 新增或调整 Controller 过滤器：阅读 [WebAPI 过滤器](WebAPIFilters.md)
- 调用模型或扩展 LLM Provider：阅读 [LLM 调用](LLM.md)
- 修改部署参数或模板：阅读 [部署配置生成器](Deployment.md)

## 阅读与示例约定

- 文档中的仓库路径以解决方案根目录为基准，命令默认也从根目录执行，另有说明的除外
- 代码块通常展示接入片段，可能省略 `using`、依赖注入和业务类型定义；示例中的 Service、DTO 和实体名不代表仓库一定存在同名实现
- Debug / Release 是编译配置，Development / Production 是宿主运行环境，两者分别决定条件编译代码和运行时环境分支，不能相互替代
- 迁移、部署与数据库 SQL 命令按对应步骤和目标环境执行，阅读或修改文档本身不要求执行这些命令

Agent 的修改范围、代码风格、授权边界与验证要求统一维护在根目录 [AGENTS.md](../AGENTS.md)，本目录专注于能力用法。核对行为时，继续检查专题文档指向的实际代码
