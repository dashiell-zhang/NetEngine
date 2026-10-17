# 本地文件日志

`Logger.LocalFile` 将业务日志先放入有界内存队列，再由后台按小时追加到本地文件。业务线程只捕获消息、异常和 Scope 快照并尝试入队，不等待队列空位，也不执行文件操作。外层日志 JSON 在后台序列化。

## 注册和配置

沿用宿主的日志注册入口，按需启用：

```csharp
builder.Logging.AddLocalFileLogger(options =>
{
    options.SaveDays = 14;
    options.QueueCapacity = 8192;
    options.MaxBufferedBytes = 48L * 1024 * 1024;
    options.ImportantQueueCapacity = 2048;
    options.ImportantMaxBufferedBytes = 16L * 1024 * 1024;
    options.BatchSize = 1000;
    options.FlushInterval = TimeSpan.FromSeconds(1);
});
```

| 配置 | 默认值 | 含义 |
|---|---|---|
| SaveDays | 14 | 文件最近写入后的保留天数，-1 禁止自动清理 |
| QueueCapacity | 8192 | 所有级别共用队列的条数上限 |
| MaxBufferedBytes | 48 MiB | 共用队列及其已取出待写批次的估算内存上限 |
| ImportantQueueCapacity | 2048 | Warning 及以上日志的额外预留队列条数上限 |
| ImportantMaxBufferedBytes | 16 MiB | 预留队列及其待写批次的估算内存上限 |
| BatchSize | 1000 | 单次取出的批量上限，满批立即写入 |
| FlushInterval | 1 秒 | 不满批时的周期性写入间隔 |

容量、字节预算和批量大小必须为正数；落盘间隔必须介于 1 毫秒与 4294967294 毫秒之间；保留天数必须为正数或 -1。注册时启用配置校验，错误配置会阻止宿主启动。缓冲配置和保留天数在对应服务创建时固定，修改配置后需重启宿主。

字节预算按字符串长度和固定开销估算，不是进程内存的硬上限。队列条数不含已经取出的批次，字节预算包含这些批次。单条日志超过对应预算时不会入队。

## 写入和恢复

日志目录为创建写入器时进程工作目录下的 `Logs`，文件名为 UTC 时间的 `yyyyMMddHH.log`。新文件使用无 BOM 的 UTF-8，每行是一个 JSON 对象，保留 `category`、`level`、`content` 三个字段。Scope 和异常仍合并在 content 中；消息对象的 ToString 只调用一次，Scope 内容在日志调用时完成快照。

后台通过 JSON 写入器直接输出三个固定字段，继续使用公共 JSON 编码器处理字符转义。单个消费者跨批次复用 UTF-8 缓冲和 JSON 写入器；每个小时段成功、失败或取消后都会清理缓冲，容量超过 64 KiB 时更换为初始 4 KiB 缓冲，避免偶发大日志长期占用大数组。64 KiB 是复用容量上限，不是单条日志长度限制。

文件追加使用独立的 FileStream 写入缓冲，按实际待写小时段的数据量自动选择：包含固定字段、换行及保守转义开销的估算值不超过 16 KiB 时使用 16 KiB，否则使用 64 KiB。大段先通过字符串长度快速判定，只有可能较小的段才扫描字符估算；中文、控制字符、引号和反斜杠按保守开销计算，避免仅按日志条数判断。这个选择只影响文件写入缓冲大小，不改变队列容量、日志内容或失败重试语义，无需新增配置。

所有级别先尝试共用队列；共用队列满或预算不足时，Warning 及以上再尝试预留队列。后台优先消费预留队列，因此不保证不同级别之间的全局写入顺序。两个缓冲均不能接收时立即返回，并累计未接收计数，不让磁盘故障把业务线程拖入等待。

后台按连续的同小时记录段写入，每段写入前记下文件长度。只有写入、刷新和关闭都成功，才释放这一段的预算并从批次移除。追加失败时保留该段和原始文件长度，按 1、2、4、8、16、30 秒退避重试。重试先截回原长度，再写入尚未确认的记录；前面已经成功的小时段不再重写。如果等待重试的文件被外部删除或截短，继续报告失败，不静默重建文件。

启动时检查严格符合本组件命名规则的小时文件；开始新一段追加时，若目标文件和长度仍与当前写入器上次确认的结果一致，则复用检查结果，否则重新检查尾部。末尾不是换行符时，先将最后一个换行符之后的原始字节完整复制、刷新并关闭到 `<小时>.log.tail-<随机值>.rejected`，成功后才截回完整行边界。保存失败时不截短原文件。没有末尾换行符的完整 JSON 也按未完成尾部处理，保留供人工检查，不自动补写。恢复只处理尾部边界，不校验或修复中间各行。

正常停机先停止接收新日志，并在宿主停机期限内继续排空和重试。期限耗尽后取消后台操作，释放内存预算并累计未确认日志数量；已写出的字节保留，残缺尾部由下次启动恢复。

## 自动清理范围

清理任务启动后运行一次，此后约每天执行一次，且只扫描 Logs 顶层：

- 仅匹配合法日期的 `yyyyMMddHH.log` 文件
- 以最近写入时间 LastWriteTimeUtc 判断过期，防止刚补写的历史小时文件被删除
- 清理与追加、尾部恢复共用互斥保护；正在写入或等待重试的目标文件不会被清理
- 数据库日志批次、其他名称的日志、临时文件、子目录和 `.rejected` 文件不属于清理范围

尾部保留文件需要人工排查和清理。不要让多个进程或多个独立写入器共享同一个 Logs 目录，也不要在运行时外部截短、替换或删除小时文件；追加回退位置只由当前写入器维护。

## 监控及可靠性边界

Meter 名称为 `Logger.LocalFile`：

| 指标 | 含义 |
|---|---|
| logger.localfile.buffered | 当前未确认落盘的日志条数 |
| logger.localfile.buffered.bytes | 当前缓冲的估算字节数 |
| logger.localfile.dropped | 按 level 累计未接收或停机时未确认落盘的条数 |
| logger.localfile.failures | 按 operation 累计 write、recovery、shutdown、cleanup 阶段失败次数 |

后台失败还会向标准错误限频输出阶段与异常类型，同一阶段最多约 30 秒一次，不通过日志管线再次记录，也不输出业务日志内容。未确认计数可能包含已经写出部分字节的日志，不能直接视为磁盘上缺失的准确条数。

“已入队”不等于“已持久化”。异常退出可能丢失仍在内存中的日志，持续磁盘故障超过缓冲容量时也会舍弃新日志；停机超时可能留下未写完的记录。FlushAsync 不提供断电级持久化保证。当前策略优先不阻塞业务，使用有界缓冲、重要日志预留、重试、尾部保留和正常停机排空尽量减少损失，不承诺绝对零丢失。

实现入口：[LocalFileLogger](../Infrastructure/Logger.LocalFile/LocalFileLogger.cs)、[LocalFileLogWriter](../Infrastructure/Logger.LocalFile/LocalFileLogWriter.cs)、[LocalFileStore](../Infrastructure/Logger.LocalFile/LocalFileStore.cs)、[LogClearTask](../Infrastructure/Logger.LocalFile/Tasks/LogClearTask.cs)。此组件独立于 [数据库日志](DatabaseLogger.md)，不会修改数据库日志写入或 EF 调用栈处理。

