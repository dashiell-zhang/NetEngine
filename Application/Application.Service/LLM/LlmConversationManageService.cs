using Application.Model.LLM.LlmConversation;
using Application.Model.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository;
using SourceGenerator.Runtime.Attributes;

namespace Application.Service.LLM;

[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class LlmConversationManageService(DatabaseContext db)
{

    /// <summary>
    /// 获取 LLM 调用日志列表
    /// </summary>
    public async Task<PageListDto<LlmConversationDto>> GetLlmConversationListAsync(LlmConversationPageRequestDto request)
    {
        PageListDto<LlmConversationDto> result = new();

        var query = db.LlmConversation
            .AsNoTracking()
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
            query = query.Where(t => t.CreateTime >= request.StartTime.Value);
        }

        if (request.EndTime.HasValue)
        {
            query = query.Where(t => t.CreateTime <= request.EndTime.Value);
        }

        result.Total = await query.CountAsync();

        if (result.Total != 0)
        {
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
                .Skip(request.Skip())
                .Take(request.PageSize)
                .ToListAsync();
        }

        return result;
    }
}
