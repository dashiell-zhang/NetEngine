using Common;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using QueueTask.Database.Models;
using System.Text.Json;

namespace QueueTask.Database;

/// <summary>
/// 在固定业务主库提供队列查询、重试及任务配置管理，不启动执行器
/// </summary>
public sealed class QueueTaskManagement<TContext>(IDbContextFactory<TContext> factory, IdService idService) where TContext : DbContext
{

    /// <summary>
    /// 带参定时任务定义的保留标识，运行实例不能使用该值
    /// </summary>
    public const string ArgsDefaultParameter = "__args_default__";


    /// <summary>
    /// 分页查询本模块队列，返回已经脱离上下文的记录
    /// </summary>
    public async Task<(int Total, List<Models.QueueTask> Items)> QueryTasksAsync(int skip, int take, bool? isSuccess = null, DateTimeOffset? start = null, DateTimeOffset? end = null, string? parameterKeyword = null, CancellationToken cancellationToken = default)
    {

        ValidatePage(skip, take);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<Models.QueueTask>().AsNoTracking();
        if (isSuccess is not null)
        {
            query = isSuccess.Value ? query.Where(task => task.SuccessTime != null) : query.Where(task => task.SuccessTime == null);
        }
        if (start is not null)
        {
            query = query.Where(task => task.CreateTime >= start);
        }
        if (end is not null)
        {
            query = query.Where(task => task.CreateTime <= end);
        }
        if (!string.IsNullOrWhiteSpace(parameterKeyword))
        {
            query = query.Where(task => task.Parameter != null && task.Parameter.Contains(parameterKeyword));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(task => task.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return (total, items);

    }


    /// <summary>
    /// 原子重置未成功且没有有效执行租约的任务，拒绝与正在执行的任务竞争
    /// </summary>
    public async Task RetryAsync(long id, CancellationToken cancellationToken = default)
    {

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var task = await QueueTaskStore<TContext>.LockTaskAsync(db, id, cancellationToken) ?? throw new CustomException("无效的 QueueTaskId");
        if (task.SuccessTime is not null)
        {
            throw new CustomException("已成功的任务不允许重试");
        }
        if (task.Status == QueueTaskStatus.Running && task.LeaseExpireTime > DateTimeOffset.UtcNow)
        {
            throw new CustomException("任务仍在执行，请等待执行结束或租约到期后重试");
        }

        task.PlanTime = null;
        task.Count = 0;
        task.FirstTime = null;
        task.LastTime = null;
        task.ChildSuccessTime = null;
        task.Status = QueueTaskStatus.Pending;
        task.WorkerId = null;
        task.LeaseExpireTime = null;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

    }


    /// <summary>
    /// 分页读取本模块的任务配置
    /// </summary>
    public async Task<(int Total, List<TaskSetting> Items)> QuerySettingsAsync(int skip, int take, string? category = null, CancellationToken cancellationToken = default)
    {

        ValidatePage(skip, take);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<TaskSetting>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(setting => setting.Category == category);
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(setting => setting.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return (total, items);

    }


    /// <summary>
    /// 更新运行配置但不改变定义、模板和带参实例的身份
    /// </summary>
    public async Task UpdateSettingAsync(long id, long actorUserId, string? parameter, int? semaphore, string? cron, bool isEnable, string? remark, CancellationToken cancellationToken = default)
    {

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var setting = await db.Set<TaskSetting>().SingleOrDefaultAsync(setting => setting.Id == id, cancellationToken) ?? throw new CustomException("无效的 taskSettingId");
        if (semaphore <= 0)
        {
            throw new CustomException("任务并发值必须大于零");
        }
        if (setting.Category == "ScheduleTask")
        {
            if (setting.Parameter is null || setting.Parameter == ArgsDefaultParameter)
            {
                if (parameter != setting.Parameter)
                {
                    throw new CustomException("任务定义或参数模板不能修改为运行实例，请通过新增带参任务创建实例");
                }
            }
            else
            {
                ValidateParameter(parameter);
            }
        }

        setting.Parameter = parameter;
        setting.Semaphore = semaphore;
        setting.Cron = setting.Category == "ScheduleTask" ? NormalizeCron(cron) : cron;
        setting.IsEnable = isEnable;
        setting.Remark = remark;
        setting.UpdateUserId = actorUserId;
        await db.SaveChangesAsync(cancellationToken);

    }


    /// <summary>
    /// 列出本模块已经同步的带参定时任务模板名称
    /// </summary>
    public async Task<List<string>> GetArgsScheduleNamesAsync(CancellationToken cancellationToken = default)
    {

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Set<TaskSetting>().AsNoTracking().Where(setting => setting.Category == "ScheduleTask" && setting.Parameter == ArgsDefaultParameter)
            .Select(setting => setting.Name).Distinct().OrderBy(name => name).ToListAsync(cancellationToken);

    }


    /// <summary>
    /// 为已有带参模板创建独立运行实例，操作人由模块应用传入
    /// </summary>
    public async Task<long> CreateScheduleAsync(long actorUserId, string name, string parameter, string? cron, bool isEnable, string? remark, CancellationToken cancellationToken = default)
    {

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ValidateParameter(parameter);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (!await db.Set<TaskSetting>().AnyAsync(setting => setting.Category == "ScheduleTask" && setting.Name == name && setting.Parameter == ArgsDefaultParameter, cancellationToken))
        {
            throw new CustomException("带参任务模板不存在，请先确认 TaskService 已同步任务配置");
        }
        var setting = new TaskSetting
        {
            Id = idService.GetId(),
            Category = "ScheduleTask",
            Name = name,
            Parameter = parameter,
            Cron = NormalizeCron(cron),
            IsEnable = isEnable,
            Remark = remark,
            CreateUserId = actorUserId
        };
        db.Set<TaskSetting>().Add(setting);
        await db.SaveChangesAsync(cancellationToken);
        return setting.Id;

    }


    /// <summary>
    /// 空白 Cron 使用声明默认值，非空配置必须通过调度解析器校验
    /// </summary>
    private static string? NormalizeCron(string? cron)
    {

        if (string.IsNullOrWhiteSpace(cron))
        {
            return null;
        }
        cron = cron.Trim();
        try
        {
            CronHelper.Validate(cron);
            return cron;
        }
        catch (FormatException)
        {
            throw new CustomException("Cron 表达式无效");
        }

    }


    /// <summary>
    /// 拒绝空参数、模板标识和无效 JSON，及时释放解析资源
    /// </summary>
    private static void ValidateParameter(string? parameter)
    {

        if (string.IsNullOrWhiteSpace(parameter) || parameter == ArgsDefaultParameter)
        {
            throw new CustomException("任务参数必须是合法 JSON");
        }
        try
        {
            using var document = JsonDocument.Parse(parameter);
        }
        catch (JsonException)
        {
            throw new CustomException("任务参数必须是合法 JSON");
        }

    }


    /// <summary>
    /// 校验分页范围，避免负数偏移或无界查询
    /// </summary>
    private static void ValidatePage(int skip, int take)
    {

        if (skip < 0 || take is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "分页偏移不能为负数，每页应为 1 至 100 条");
        }

    }

}
