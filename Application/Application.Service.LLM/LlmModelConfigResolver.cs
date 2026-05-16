using LLM;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository;
using SourceGenerator.Runtime.Attributes;

namespace Application.Service.LLM;

/// <summary>
/// LLM 模型配置解析器实现
/// </summary>
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class LlmModelConfigResolver(DatabaseContext db) : ILlmModelConfigResolver
{

    /// <summary>
    /// 获取指定模型的配置信息
    /// </summary>
    public async Task<LlmModelConfig?> GetConfigAsync(long modelId, CancellationToken cancellationToken = default)
    {
        return await db.LlmModel
            .AsNoTracking()
            .Where(t => t.Id == modelId && t.IsEnable && t.DeleteTime == null)
            .Select(t => new LlmModelConfig
            {
                Endpoint = t.Endpoint,
                ApiKey = t.ApiKey,
                ModelId = t.ModelId,
                ProtocolType = t.ProtocolType
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

}
