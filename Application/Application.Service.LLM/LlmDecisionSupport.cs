using Application.Model.LLM.Decision;
using Common;
using LLM;
using LLM.SystemOne;

namespace Application.Service.LLM;

/// <summary>
/// 统一决策配置校验及应用响应转换，供正式调用与草稿测试共用
/// </summary>
internal static class LlmDecisionSupport
{

    /// <summary>
    /// 校验保存的决策模板，移除根级模型标识并保留其他字段
    /// </summary>
    internal static string PrepareTemplateForSave(int protocolType, string? systemPrompt, string template, string? extraBodyJson)
    {

        if (protocolType != (int)LlmProtocolType.SystemOne)
        {
            return template;
        }
        return PrepareRequest(template, systemPrompt, extraBodyJson, null).Template;

    }


    /// <summary>
    /// 使用实际参数生成经过校验的决策请求
    /// </summary>
    internal static DecisionRequest CreateRequest(string template, string? systemPrompt, string? extraBodyJson, IReadOnlyDictionary<string, string> parameters)
        => PrepareRequest(template, systemPrompt, extraBodyJson, parameters).Request;


    /// <summary>
    /// 保存时校验模板，调用时校验参数替换后的请求
    /// </summary>
    private static (DecisionRequest Request, string Template) PrepareRequest(string template, string? systemPrompt, string? extraBodyJson, IReadOnlyDictionary<string, string>? parameters)
    {

        if (!string.IsNullOrWhiteSpace(systemPrompt) || !string.IsNullOrWhiteSpace(extraBodyJson))
        {
            throw new CustomException("请在决策请求模板中填写判断说明及扩展字段");
        }
        try
        {
            var root = LlmDecisionTemplate.Parse(template, out var removedModel);
            if (parameters == null)
            {
                // 保存时也检查字段名中的非法占位符
                _ = LlmDecisionTemplate.TextValues(root).Count();
                if (removedModel)
                {
                    template = root.ToJsonString(LlmDecisionTemplate.DisplayOptions);
                }
            }
            else
            {
                LlmDecisionTemplate.Render(root, parameters);
            }
            return (SystemOneDecisionClient.CreateRequest(root), template);
        }
        catch (InvalidOperationException exception)
        {
            throw new CustomException(exception.Message);
        }

    }


    /// <summary>
    /// 将协议结果转换为不依赖基础设施的应用返回模型
    /// </summary>
    internal static LlmDecisionResponseDto Map(DecisionResponse response)
    {

        return new LlmDecisionResponseDto
        {
            Model = response.Model,
            RequestId = response.RequestId,
            InputTokens = response.Usage?.InputTokens,
            OutputTokens = response.Usage?.OutputTokens,
            LatencyMs = response.LatencyMs,
            Answers = response.Answers.ToDictionary(item => item.Key, item => new LlmDecisionAnswerDto
            {
                Type = item.Value.Type,
                Choice = item.Value.Choice,
                YesProbability = item.Value.Noul,
                Score = item.Value.Score,
                Confidence = item.Value.Confidence,
                Probabilities = item.Value.Probabilities,
                Legend = item.Value.Legend
            }, StringComparer.Ordinal)
        };

    }

}
