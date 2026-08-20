# NetEngine 使用文档

这里集中维护仓库内公共能力和工具的详细用法。根目录 `README.md` 主要介绍项目结构、快速启动和文档入口，具体使用约定以本目录文档及实际代码为准

## 文档导航

| 文档 | 适合在什么时候阅读 | 主要内容 |
|---|---|---|
| [架构与项目边界](Architecture.md) | 新增功能、调整分层或拆分项目时 | 分层职责、依赖方向、宿主引用与项目拆分原则 |
| [WebAPI 公共能力](WebAPI.md) | 调整 API 宿主、公共配置或中间件时 | 启动链路、配置、CORS、认证、Swagger 和健康检查 |
| [部署配置生成器](Deployment.md) | 需要生成 Nginx、systemd 或云效流水线配置时 | 配置项、生成命令、产物和首次部署准备 |
| [分布式锁](DistributedLock.md) | 需要防止重复执行或限制跨实例并发时 | Redis 锁、内存锁、等待与立即返回、租约续期 |
| [源码生成器](SourceGenerator.md) | 新增服务、后台服务、代理行为或 EF Core 映射时 | 自动 DI、AutoProxy、软删除过滤器、JSON 列映射 |
| [数据库读写分离](DatabaseReadWriteSeparation.md) | 迁移查询、配置读库或规划多个读副本时 | 读写上下文、连接配置、一致性边界和健康检查 |
| [PostgreSQL 分区表](PostgreSqlPartitionTable.md) | 为新实体声明雪花 ID 分区或维护后续子分区时 | 实体注解、Migration SQL、间隔调整和 Repository 自动维护 |
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

## 面向 Coding Agent 的扫描约定

开始修改对应能力前，应先阅读相关文档，并继续检查文档链接的实际代码。文档用于说明稳定的使用约定，代码仍是最终行为依据

- 新增功能时先根据 [架构与项目边界](Architecture.md) 确认代码放置位置和引用方向
- 新增服务时优先使用 `[RegisterService]`，不要重复手写 DI 注册
- 新增后台服务时先确认 `BatchRegisterBackgroundServices()` 是否已经覆盖
- 新增跨进程并发控制时使用 Redis 锁，不要使用内存锁代替分布式锁
- 新增任务时先区分定时任务和队列任务，并确认任务已经启用
- 新增过滤器时先确认它属于表现层职责，并检查所需服务和中间件是否已经注册
- 业务调用 LLM 时优先注入 `LlmInvokeService`，不要直接依赖具体 Provider 客户端
- 修改部署产物时编辑 `Templates` 或 `deploysettings.json`，不要直接编辑 `Generated`
