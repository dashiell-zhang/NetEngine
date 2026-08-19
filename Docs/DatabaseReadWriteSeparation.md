# 数据库读写分离

本文说明 NetEngine 当前的数据库读写分离实现、上下文选择方式、只读连接配置和运行边界。具体运行行为以三个宿主的 `Program.cs` 与 `Repository` 中的上下文实现为准

## 当前实现

项目使用两个 EF Core 上下文访问同一套实体模型：

| 上下文 | 职责 | 默认行为 |
|---|---|---|
| `DatabaseContext` | 主库读写、事务、写入前置查询和强一致查询 | 正常实体跟踪，可执行保存和其他写操作 |
| `ReadDatabaseContext` | 展示、检索、历史记录和允许最终一致的纯查询 | 全局使用 `QueryTrackingBehavior.NoTracking` |

`ReadDatabaseContext` 是 `DatabaseContext` 的空派生上下文，复用实体集合、映射、软删除过滤器和 JSON 列映射。独立类型的作用是让依赖注入能够为读取连接建立单独的数据源、连接池和 EF Core 选项，并在应用服务中明确表达查询意图

以下宿主均已注册两个上下文：

- `Presentation/Client.WebAPI`
- `Presentation/Admin.WebAPI`
- `Presentation/TaskService`

读取上下文的注册流程如下：

1. 读取 `ConnectionStrings:dbReadConnection`
2. 配置为空时回退到 `ConnectionStrings:dbConnection`
3. 保留连接字符串中原有的 `Options`，并自动追加 `-c default_transaction_read_only=on`
4. 建立独立的 Npgsql 数据源和 EF Core 上下文池
5. 为读取上下文启用全局不跟踪查询

因此本地开发可以暂时不配置独立读库，已经迁移的查询仍能运行；生产环境应明确配置读副本或只读账号

## 连接字符串配置

三个宿主分别在自己的 `appsettings.json` 和 `appsettings.Development.json` 中读取相同的配置名称：

```json
{
  "ConnectionStrings": {
    "dbConnection": "Host=primary-db;Port=5432;Database=webcore;Username=webcore_write;Password=***;Maximum Pool Size=30",
    "dbReadConnection": "Host=read-db;Port=5432;Database=webcore;Username=webcore_read;Password=***;Maximum Pool Size=30"
  }
}
```

### 本地开发或暂未部署读库

可以把 `dbReadConnection` 保持为空：

```json
{
  "ConnectionStrings": {
    "dbConnection": "Host=127.0.0.1;Port=5432;Database=webcore;Username=postgres;Password=123456;Maximum Pool Size=30",
    "dbReadConnection": ""
  }
}
```

此时两个上下文连接同一主库，但使用彼此独立的数据源和连接池。读取连接仍会自动启用默认只读事务，这种模式适合开发和迁移过渡，不等于已经把查询流量分配到独立服务器

### 单个只读数据库服务器

生产环境最直接的配置是让 `dbReadConnection` 指向一个物理只读副本或云数据库提供的只读地址：

```text
Host=read-db;Port=5432;Database=webcore;Username=webcore_read;Password=***;Maximum Pool Size=30
```

若云数据库已经提供由平台维护的统一只读地址，项目只需配置该地址，节点选择、故障转移和负载均衡由云平台负责

### 多个只读数据库服务器

当前三个宿主使用 `NpgsqlDataSourceBuilder.Build()`，所以当前版本只支持单个读取地址。仅把 `Host` 改为逗号分隔的多个服务器还不够；启用 Npgsql 多主机能力时，需要把三个宿主的读取数据源统一改为 `BuildMultiHost()`

完成代码调整后，多个物理只读副本可以使用以下形式：

```text
Host=read-db-1:5432,read-db-2:5432,read-db-3:5432;Database=webcore;Username=webcore_read;Password=***;Load Balance Hosts=true;Target Session Attributes=standby;Host Recheck Seconds=10;Maximum Pool Size=30
```

如果业务允许读副本全部不可用时回退主库，可以把主库也加入地址列表，并使用 `prefer-standby`：

```text
Host=primary-db:5432,read-db-1:5432,read-db-2:5432;Database=webcore;Username=webcore_read;Password=***;Load Balance Hosts=true;Target Session Attributes=prefer-standby;Host Recheck Seconds=10;Maximum Pool Size=30
```

相关参数含义：

| 参数 | 作用 |
|---|---|
| `Load Balance Hosts=true` | 新建物理连接时在符合条件的主机之间轮换，而不是总从列表第一个主机开始 |
| `Target Session Attributes=standby` | 只选择 PostgreSQL 物理备用节点，不回退主库 |
| `Target Session Attributes=prefer-standby` | 优先选择物理备用节点，没有可用备用节点时允许选择主库 |
| `Host Recheck Seconds=10` | 主机判定不可用后，经过指定秒数再重新检查 |

项目会自动追加 `Options=-c default_transaction_read_only=on`，配置文件不需要重复填写。也不建议使用 `Target Session Attributes=read-only` 代替 `standby` 来识别物理副本，因为项目主动设置的默认只读事务会让可写主库连接也表现为默认只读

多主机连接只负责选择建立物理连接的目标。已经发往某个节点的 SQL 如果执行失败，Npgsql 不会自动在另一节点重放该命令；需要重试时应由应用的重试策略重新执行整个安全的只读操作

## 应用代码中的用法

### 纯查询服务

整个服务只包含允许最终一致的查询时，直接注入 `ReadDatabaseContext`：

```csharp
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class LogManageService(ReadDatabaseContext readDb)
{

    /// <summary>
    /// 获取日志列表
    /// </summary>
    public Task<List<LogDto>> GetLogListAsync()
    {

        return readDb.Log.OrderByDescending(c => c.Id).Select(c => new LogDto
        {
            Id = c.Id,
            Content = c.Content,
            CreateTime = c.CreateTime
        }).ToListAsync();

    }

}
```

读取上下文已全局启用 `NoTracking`，一般不需要为每条查询重复调用 `AsNoTracking()`

### 混合读写服务

同一个服务既有写操作又有适合迁移的展示查询时，可以同时注入两个上下文，并使用 `writeDb`、`readDb` 明确命名：

```csharp
public class ExampleService(DatabaseContext writeDb, ReadDatabaseContext readDb)
{

    /// <summary>
    /// 获取允许最终一致的展示详情
    /// </summary>
    public Task<ArticleDto?> GetPublishedArticleAsync(long id)
    {

        return readDb.Article.Where(c => c.Id == id && c.IsDisplay).Select(c => new ArticleDto
        {
            Id = c.Id,
            Title = c.Title
        }).FirstOrDefaultAsync();

    }


    /// <summary>
    /// 更新文章
    /// </summary>
    public async Task UpdateArticleTitleAsync(long id, string title)
    {

        Article article = await writeDb.Article.FirstAsync(c => c.Id == id);
        article.Title = title;
        await writeDb.SaveChangesAsync();

    }

}
```

这是上下文选择方式示例，不代表仓库存在名为 `ExampleService` 的服务。不要把从 `ReadDatabaseContext` 查询出的实体直接交给写上下文更新；写操作应在主库上下文中重新查询目标实体，或者让读取查询直接投影为 DTO

### 上下文选择规则

| 场景 | 推荐上下文 | 原因 |
|---|---|---|
| 公共展示、普通列表、历史日志、低频字典 | `ReadDatabaseContext` | 短时间旧数据通常只影响展示 |
| 创建、更新、删除及其前置查询 | `DatabaseContext` | 需要实体跟踪和主库最新状态 |
| 唯一性、存在性、并发和状态流转判断 | `DatabaseContext` | 副本延迟可能导致重复写入或错误决策 |
| 登录、权限、密码、Token、支付和订单 | `DatabaseContext` | 属于安全或资金边界 |
| 队列抢占、租约、任务状态和运行配置 | `DatabaseContext` | 需要强一致状态 |
| 保存后立即刷新的管理页面 | 通常使用 `DatabaseContext` | 保证读取自己刚完成的写入 |
| 与刚才写入无关、允许最终一致的后续展示 | `ReadDatabaseContext` | 可以承受复制延迟 |

不要为了切换上下文增加通用 Repository 包装层或到处传递 `usePrimary` 布尔参数。选择哪个上下文属于具体用例的一致性要求，应在应用服务的方法中明确体现

## 保存后立即刷新的处理

独立读副本通常采用异步复制，保存成功不代表读副本已经同步。创建、编辑或删除后立即通过读库刷新，可能出现新数据暂时不存在、旧值仍显示或已删除数据短暂存在

项目建议按业务场景处理：

- 后台管理的新增、编辑、删除页面需要立即看到最新结果时，对应详情和列表查询保留主库
- 公共展示、历史查询等允许最终一致的页面可以继续使用读库，由用户下一次刷新自然看到新数据
- 不使用固定 `Task.Delay` 等待复制，延迟时间无法保证同步已经完成
- “读库未命中再回主库”只适合解决新建记录短暂未同步，不足以识别旧值、旧列表和已删除记录
- 若未来必须对同一查询动态回主库，应封装在具体业务方法中，并限定明确的触发条件和持续时间

## 只读保护边界

`ReadDatabaseContext` 本身不重写 `SaveChanges`，也不会在代码层阻止以下操作：

- `SaveChanges` 和 `SaveChangesAsync`
- `ExecuteUpdate` 和 `ExecuteDelete`
- `ExecuteSqlRaw`、`ExecuteSqlInterpolated` 等原始 SQL
- 直接取得连接后执行的数据库命令

宿主自动追加的 `-c default_transaction_read_only=on` 会让读取连接上的新事务默认只读，可以拦住大部分误写。但它是会话默认值，不是数据库权限；当连接的是可写主库且账号具有写权限时，调用方仍可能主动关闭只读设置

生产环境应把最终边界放在 PostgreSQL：

- 优先连接物理只读副本，数据库自身不接受普通写入
- 读取账号仅授予数据库连接、Schema 使用和业务表查询权限
- 读取账号不要使用表所有者、超级用户或包含写权限的角色
- 新增表后同步维护读取账号的默认查询权限
- 主库回退方案也必须使用只读账号，否则 `prefer-standby` 回退主库时只剩默认只读事务这一层保护

## 健康检查

WebAPI 公共健康检查同时包含：

- `DatabaseHealthCheck`：验证主库连接
- `ReadDatabaseHealthCheck`：验证读取连接

`Client.WebAPI` 和 `Admin.WebAPI` 通过 `/healthz` 暴露检查结果。`TaskService` 注册了读取上下文，但当前不是 HTTP 宿主，没有暴露 `/healthz`

使用多主机数据源后，读取健康检查只能证明当前可以从候选主机中获得一个可用连接，不代表每一个只读节点都健康。生产环境还应由数据库或监控平台分别检查节点状态、复制延迟和连接数

## 当前迁移状态

目前已经迁移到 `ReadDatabaseContext` 的低风险纯查询服务包括：

- `BaseService`
- `LogManageService`
- `LlmConversationManageService`

其他候选调用、风险分级和建议迁移顺序见 [只读数据库上下文迁移检查清单](ReadDatabaseContextMigrationChecklist.md)

## 上线检查清单

- 三个宿主都提供正确的 `dbConnection` 和 `dbReadConnection`
- 读取账号不能执行插入、更新、删除、建表和改表
- 读副本的 Schema、迁移版本和主库一致
- 软删除过滤器、JSON 列和分页查询在读库上行为正常
- 健康检查能分别识别主库和读取连接故障
- 已明确监控 PostgreSQL 复制延迟、连接池使用量和读库错误率
- 已确认读库不可用时是直接失败、业务重试还是允许回主库
- 保存后立即刷新的管理用例仍能读取到主库最新状态
- 多读库配置上线前，三个宿主均已切换到 `BuildMultiHost()` 并完成故障转移验证

## 相关文档

- [WebAPI 公共能力](WebAPI.md)
- [TaskService](TaskService.md)
- [只读数据库上下文迁移检查清单](ReadDatabaseContextMigrationChecklist.md)
- [Npgsql 故障转移和负载均衡](https://www.npgsql.org/doc/failover-and-load-balancing.html)
- [Npgsql 连接字符串参数](https://www.npgsql.org/doc/connection-string-parameters)
