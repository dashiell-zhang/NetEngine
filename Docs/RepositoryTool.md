# 数据库工具

`Repository.Tool` 是一次性工具和 EF Core 设计时入口，不启动业务宿主、日志、事件、ID 或任务后台服务。无参数运行只显示帮助，不访问数据库

## 项目目录

| 目录或文件 | 职责 |
|---|---|
| `Program.cs` | 命令行入口与操作选择 |
| `appsettings.json` | Business、Log、EventBus 连接配置 |
| `Configuration/ToolDatabaseOptions.cs` | 配置读取、参数校验及上下文选项 |
| `Factories` | 三个写上下文的 EF Core 设计时工厂 |
| `Scripts/JsonIndexScript.cs` | JSON 索引差异 SQL 生成 |
| `Migrations` | 首次生成迁移后创建，保存迁移和模型快照 |

设计时工厂由 EF Core 在工具程序集中发现，目录分类不改变 `Add-Migration -Context` 和 `Update-Database -Context` 的使用方式

## 配置与目标

默认读取项目中的 [appsettings.json](../Repository.Tool/appsettings.json)，其中 Business、Log、EventBus 对应三个写模型。当前三项沿用原工具连接，指向同一个数据库；需要拆库时分别修改对应项。所选目标连接缺失时直接报错，不回退其他模型的连接

配置文件随构建和发布复制到输出目录，设计时工厂按工具程序集位置读取，不依赖程序包管理控制台或终端的当前目录。修改项目配置后正常构建即可生效，不需要额外设置配置路径，也不读取 WebAPI 的配置

迁移通过 EF Core 的 `-Context` 选择模型，JSON 索引命令通过 `--target` 选择模型。工具配置统一由 appsettings.json 管理

## EF Core 迁移

在本机使用 Debug，选择 Repository.Tool 为目标及启动项目，并明确指定写上下文。设计时工厂分别创建精确类型的选项，不通过启动 WebAPI 获得配置

| Context | 初始迁移示例名 | 建议输出目录 | public 中的历史表 |
|---|---|---|---|
| `Repository.DatabaseContext` | `Init1` | `Migrations` | `__EFMigrationsHistory_Business` |
| `Logger.Database.Persistence.LogDatabaseContext` | `Init2` | `Migrations/LogDatabase` | `__EFMigrationsHistory_Log` |
| `EventBus.Database.Persistence.EventBusDatabaseContext` | `Init3` | `Migrations/EventBusDatabase` | `__EFMigrationsHistory_EventBus` |

当前模板未包含迁移文件或模型快照。首次初始化空数据库时，先为所需上下文生成初始迁移，再应用到数据库。迁移依靠上下文关联隔离，目录本身不代表隔离。读上下文没有迁移工厂。QueueTask 和 TaskSetting 随其所属业务模型迁移

### Visual Studio 程序包管理控制台

把 Repository.Tool 设为启动项目，并在程序包管理控制台的默认项目中选择 Repository.Tool，解决方案配置使用 Debug。工具自动读取 appsettings.json，无需额外设置配置路径。当前上下文名称不重名，可使用下面的短名称；若将来出现重名，改用表中的完整名称

**首次使用模板且目标为空数据库时，生成并应用初始迁移。每次 Add-Migration 后先检查生成内容，再执行对应 Update-Database：**

```powershell
Add-Migration Init1 -Context DatabaseContext -OutputDir Migrations
Update-Database -Context DatabaseContext

Add-Migration Init2 -Context LogDatabaseContext -OutputDir Migrations/LogDatabase
Update-Database -Context LogDatabaseContext

# 使用数据库事件后端时执行，Redis 后端可以省略
Add-Migration Init3 -Context EventBusDatabaseContext -OutputDir Migrations/EventBusDatabase
Update-Database -Context EventBusDatabaseContext
```

即使三项连接指向同一个物理数据库，也要分别更新三个上下文，业务迁移不会自动创建日志或事件表。每个上下文使用自己的迁移历史表，已应用的迁移不会因为再次执行 Update-Database 而重复建表

已有迁移文件时，初始化其他空数据库或应用尚未执行的迁移，直接执行 Update-Database，不重复生成初始迁移：

```powershell
Update-Database -Context DatabaseContext
Update-Database -Context LogDatabaseContext

# 使用数据库事件后端时执行，Redis 后端可以省略
Update-Database -Context EventBusDatabaseContext
```

后续只为发生模型变化的上下文使用新的迁移名称，例如：

```powershell
Add-Migration AddOrderRemark -Context DatabaseContext
Update-Database -Context DatabaseContext
```

Add-Migration 只生成迁移代码，Update-Database 才会操作数据库结构。不指定 OutputDir 时沿用该上下文已有迁移目录；迁移文件、Designer 文件和 ModelSnapshot 应一起提交

数据库已有表时，不适用上述空库初始化流程。应使用与该数据库迁移历史对应的迁移文件和快照，不能仅因本地文件缺失就重新生成初始迁移并应用

### 命令行

```powershell
# 首次使用且目标为空数据库时生成业务初始迁移
dotnet ef migrations add Init1 --project Repository.Tool --startup-project Repository.Tool --context Repository.DatabaseContext --configuration Debug --output-dir Migrations

# 业务模型发生变化时生成新的增量迁移
dotnet ef migrations add AddOrderRemark --project Repository.Tool --startup-project Repository.Tool --context Repository.DatabaseContext --configuration Debug

dotnet ef migrations script --project Repository.Tool --startup-project Repository.Tool --context Repository.DatabaseContext --configuration Debug

# 审查目标和生成脚本后，再按需要单独执行数据库更新
dotnet ef database update --project Repository.Tool --startup-project Repository.Tool --context Repository.DatabaseContext --configuration Debug
```

初始迁移和增量迁移命令按所处阶段选择，不连续执行。日志和事件使用表中对应的完整 Context 名及目录分别操作。共享组件库由一个明确的工具部署入口管理，多个服务使用同一份组件迁移产物，不能各自生成互不一致的快照

同物理库部署时各模型仍只拥有自己的表。拆分已有模型时需审查删除表等差异，工具不会自动搬迁历史数据，不在常规启动调用 Migrate 或 EnsureCreated

## JSON 索引差异

```powershell
dotnet run --project Repository.Tool -- json-index --target Business
```

此命令连接选定库，只读取索引目录并输出 SQL，不执行 DDL。EF 模型决定实际 schema、表名、列名和 JSON 属性名；未指定 schema 时按该连接的 search_path 解析

比较范围只包括当前模型拥有的表以及工具保留的 `jsonbIX_` 加哈希名称。模型外表和人工命名索引不列为删除候选。不要手工修改工具拥有的索引或复用其保留名称；调整索引声明后重新生成差异并审查执行

支持映射为 JSON 的复杂属性标量路径，拒绝集合路径和不支持的类型。分区表唯一索引必须包含分区键。读取索引目录不需要写入用的分区快照；通过其他代码保存分区实体时仍必须加载目标快照
