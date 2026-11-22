using Application.Interface.LLM;
using Application.Model.LLM.LlmConversation;
using Application.Model.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository;
using SourceGenerator.Runtime.Attributes;

namespace Application.Service.LLM;

/// <summary>
/// LLM 对话记录管理服务
/// </summary>
[RegisterService(typeof(ILlmConversationManageService), Lifetime = ServiceLifetime.Scoped)]
public class LlmConversationManageService(ReadDatabaseContext readDb) : ILlmConversationManageService
{

    /// <summary>
    /// 获取 LLM 调用日志列表
    /// </summary>
    public async Task<PageListDto<LlmConversationDto>> GetLlmConversationListAsync(LlmConversationPageRequestDto request)
    {

        PageListDto<LlmConversationDto> result = new();

        var query = readDb.LlmConversation
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();

            query = query.Where(t =>
                (t.LlmApp != null && (t.LlmApp.Code.Contains(keyword) || t.LlmApp.Name.Contains(keyword))) ||
                (t.SystemContent != null && t.SystemContent.Contains(keyword)) ||
                (t.UserContent != null && t.UserContent.Contains(keyword)) ||
                (t.AssistantContent != null && t.AssistantContent.Contains(keyword)));
        }

        if (request.StartTime.HasValue)
        {
            // PostgreSQL 时间戳参数统一使用 UTC，保留输入偏移表示的同一时刻
            var startTime = request.StartTime.Value.ToUniversalTime();
            query = query.Where(t => t.CreateTime >= startTime);
        }

        if (request.EndTime.HasValue)
        {
            var endTime = request.EndTime.Value.ToUniversalTime();
            query = query.Where(t => t.CreateTime <= endTime);
        }

        result.Total = await query.CountAsync();

        if (result.Total != 0)
        {
            var skip = request.Skip();
            if (skip >= result.Total)
            {
                result.List = [];
                return result;
            }

            result.List = await query
                .OrderByDescending(t => t.Id)
                .Select(t => new LlmConversationDto
                {
                    Id = t.Id,
                    LlmAppId = t.LlmAppId,
                    LlmAppCode = t.LlmApp.Code,
                    LlmAppName = t.LlmApp.Name,
                    CreateUserId = t.CreateUserId,
                    CreateUserName = t.CreateUser == null ? null : t.CreateUser.Name,
                    CreateTime = t.CreateTime,
                    SystemContent = t.SystemContent,
                    UserContent = t.UserContent,
                    AssistantContent = t.AssistantContent
                })
                .Skip(skip)
                .Take(request.PageSize)
                .ToListAsync();
        }

        return result;
    }
}
