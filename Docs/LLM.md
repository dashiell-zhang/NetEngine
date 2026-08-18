# LLM 调用

仓库将 LLM 能力分为基础设施协议适配和应用层业务入口两部分：

- `Infrastructure/LLM`：统一请求响应模型、协议客户端和动态客户端工厂
- `Application/Application.Service.LLM`：模型配置、应用配置、提示词渲染、调用编排和对话记录

业务代码应优先注入 `Application.Service.LLM.LlmInvokeService`，按 LLM 应用的 `Code` 发起调用。除非正在扩展新的协议或 Provider，不要在业务代码中直接创建 `ILlmClient`

## 调用链

```text
业务服务
  ↓ LlmInvokeService + LlmApp.Code
LlmApp：提示词模板、额外参数、关联模型
  ↓ LlmModelId
LlmModel：Endpoint、ApiKey、ModelId、ProtocolType
  ↓ ILlmClientFactory
Chat / Responses / Anthropic 客户端
  ↓
模型服务商 API
```

模型和应用配置保存在 PostgreSQL，不从 `appsettings.json` 的 `LLM:Providers` 配置节读取

## 宿主接入

需要调用 LLM 的宿主应引用 `Application.Service.LLM`，并完成以下注册：

```csharp
builder.Services.BatchRegisterServices();
builder.Services.AddHttpClient();
builder.Services.AddLlmClientFactory();
```

- `BatchRegisterServices()` 注册 `LlmInvokeService`、`LlmModelConfigResolver` 等 Scoped 服务
- `AddHttpClient()` 提供 `IHttpClientFactory`
- `AddLlmClientFactory()` 注册单例 `ILlmClientFactory`

宿主还需要已有的 `DatabaseContext`、`IdService` 和 `IUserContext` 注册。管理模型与应用配置时还会使用 `IDistributedLock`

当前 `Admin.WebAPI` 和 `Client.WebAPI` 已完成上述引用和注册。`TaskService` 没有引用 `Application.Service.LLM`，因此不会自动注册 LLM 应用服务；如果任务需要调用 LLM，应先评估宿主边界，再显式增加项目引用和基础服务注册

## 第一次配置

完成数据库初始化并启动 `Admin.WebAPI`、`Admin.App` 后，按以下顺序配置：

1. 在 `/operations/llmmodel` 创建模型配置
2. 在 `/operations/llmapp` 创建 LLM 应用配置并测试
3. 保存并启用模型与应用
4. 在业务代码中使用应用 `Code` 调用 `LlmInvokeService`

管理页面中的“测试调用”使用当前尚未保存的提示词和 ExtraBody，但仍会读取已保存的模型配置。该测试不会写入 `LlmApp` 或 `LlmConversation`，正式业务调用仍以已保存并启用的应用配置为准

### 模型配置

| 字段 | 说明 |
|---|---|
| `Name` | 管理后台显示名称 |
| `ModelId` | 传给供应商的实际模型标识 |
| `Endpoint` | 完整请求地址，不是只有域名的 Base URL |
| `ApiKey` | 模型服务接口密钥 |
| `ProtocolType` | 请求协议类型 |
| `IsEnable` | 是否允许解析和调用该模型 |

支持的协议：

| 值 | 协议 | Endpoint 形态示例 | 鉴权方式 |
|---:|---|---|---|
| `0` | OpenAI Chat Completions 兼容协议 | `https://provider.example/v1/chat/completions` | `Authorization: Bearer <ApiKey>` |
| `1` | OpenAI Responses API 协议 | `https://provider.example/v1/responses` | `Authorization: Bearer <ApiKey>` |
| `2` | Anthropic Messages API 协议 | `https://provider.example/v1/messages` | `x-api-key`，并发送 `anthropic-version: 2023-06-01` |

`Endpoint` 必须与所选协议匹配。HTTP 客户端调用超时时间统一为 120 秒

创建模型时 `ApiKey` 必填；编辑模型时留空表示保留原值。模型被启用的 LLM 应用引用时不能直接禁用，存在任何未删除应用时也不能删除

### LLM 应用配置

| 字段 | 说明 |
|---|---|
| `Code` | 业务调用使用的稳定标识，同一未删除应用中不可重复 |
| `Name` | 管理后台显示名称 |
| `LlmModelId` | 关联的模型配置 |
| `SystemPromptTemplate` | 可选的 System 提示词模板 |
| `PromptTemplate` | 必填的 User 提示词模板 |
| `ExtraBodyJson` | 可选的协议请求体根字段扩展 |
| `IsEnable` | 是否允许通过 `LlmInvokeService` 调用 |

业务代码依赖的是 `Code`，而不是数据库主键或模型名称。切换模型、端点或提示词时只需更新后台配置，不需要修改调用代码

启用的 LLM 应用只能关联启用的模型。`LlmInvokeService` 调用时也会同时检查应用和模型是否启用、是否已被软删除

## 提示词模板

System 和 User 模板都支持以下占位符：

```text
{{name}}
{{*question}}
{{*content | 需要总结的正文}}
```

- `{{name}}`：可选参数
- `{{*question}}`：必填参数，`*` 写在参数名前
- `|` 后内容是参数备注，用于管理页面展示，不会发送给模型
- 参数名不能包含空白、`{`、`}` 或 `|`

例如：

```text
SystemPromptTemplate:
你是一名文章编辑，请使用 {{language}} 回答

PromptTemplate:
请总结下面的内容：
{{*content | 需要总结的正文}}
```

推荐调用方使用大小写不敏感的参数字典：

```csharp
Dictionary<string, string> parameters = new(StringComparer.OrdinalIgnoreCase)
{
    ["language"] = "中文",
    ["content"] = article.Content
};
```

必填参数不存在、值为 `null` 或空白时会抛出 `CustomException`。可选参数未提供时，当前实现会保留原始 `{{key}}` 占位文本，并不会自动替换为空字符串；如果不希望占位符进入最终提示词，调用方应提供明确值

参数查找遵循传入 Dictionary 自身的比较器。使用默认 Dictionary 时区分大小写，因此推荐显式使用 `StringComparer.OrdinalIgnoreCase`

`parameters` 不能传入 `null`。没有占位符时也应传入空字典，例如 `new Dictionary<string, string>()`

## 推荐调用方式

LLM 相关业务编排建议继续放在 `Application.Service.LLM` 中，由具体业务服务注入 `LlmInvokeService`

### 只获取首条文本

大多数文本生成场景使用 `ChatContentAsync`：

```csharp
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class ArticleSummaryService(LlmInvokeService llmInvokeService)
{
    public Task<string?> SummarizeAsync(string content, CancellationToken cancellationToken)
    {
        Dictionary<string, string> parameters = new(StringComparer.OrdinalIgnoreCase)
        {
            ["content"] = content,
            ["language"] = "中文"
        };

        return llmInvokeService.ChatContentAsync("article-summary", parameters, cancellationToken);
    }
}
```

返回值来自响应中第一条 Choice 的消息文本；供应商没有返回 Choice 时结果为 `null`

### 获取完整响应

需要模型名称、响应 ID、结束原因或 Token 用量时使用 `ChatAsync`：

```csharp
var response = await llmInvokeService.ChatAsync(
    "article-summary",
    parameters,
    cancellationToken);

var content = response.Choices.FirstOrDefault()?.Message.Content;
var finishReason = response.Choices.FirstOrDefault()?.FinishReason;
var totalTokens = response.Usage?.TotalTokens;
var responseId = response.Id;
```

统一响应模型包括：

- `Model`：供应商实际返回的模型名称
- `Choices`：候选内容和结束原因
- `Usage`：输入、输出和总 Token 数，供应商未返回时可能为空
- `Id`：供应商请求或响应标识

### 流式调用

使用 `ChatStreamAsync` 消费统一的流式分片：

```csharp
var result = new StringBuilder();

await foreach (var chunk in llmInvokeService
    .ChatStreamAsync("article-summary", parameters, cancellationToken)
    .WithCancellation(cancellationToken))
{
    var delta = chunk.Choices.FirstOrDefault()?.Delta?.Content;
    if (!string.IsNullOrEmpty(delta))
    {
        result.Append(delta);
    }
}
```

`ChatStreamAsync` 只负责从上游模型服务读取并转换增量分片，不会自动把当前 WebAPI 响应变成 SSE。Controller 需要根据自己的接口协议决定是逐块输出、返回 `IAsyncEnumerable`，还是像上例一样聚合为完整文本

消费方应把请求的 `CancellationToken` 传入服务并传给 `WithCancellation`。提前停止枚举、客户端取消或流中途异常都会结束上游读取

## ExtraBodyJson

`ExtraBodyJson` 必须是 JSON 对象，用于向当前协议请求体根节点追加参数，例如：

```json
{
  "temperature": 0.2,
  "max_tokens": 1000
}
```

字段名称和含义由当前供应商及协议决定：

- Chat 兼容协议通常使用 `temperature`、`max_tokens` 等字段
- Responses 协议可能使用 `temperature`、`max_output_tokens` 等字段
- Anthropic 使用 `max_tokens`；未提供时当前客户端默认使用 `4096`

基础字段由客户端生成，不能通过 ExtraBody 覆盖：

| 协议 | 不能冲突的基础字段 |
|---|---|
| Chat | `model`、`messages`、`stream` |
| Responses | `model`、`input`、`stream` |
| Anthropic | `model`、`messages`、`stream`，存在 System 提示词时还包括 `system` |

发生字段冲突时会抛出 `InvalidOperationException`。管理端保存应用时只校验它是不是 JSON 对象，具体字段是否被供应商支持要通过“测试调用”确认

## 对话记录

成功调用后，`LlmInvokeService` 会尝试将以下内容写入 `LlmConversation`：

- LLM 应用 ID
- 渲染后的 System 提示词
- 渲染后的 User 提示词
- Assistant 返回文本
- 已认证用户的用户 ID；匿名调用时为空

非流式调用记录第一条 Choice 的文本。流式调用只有在完整消费到带 `FinishReason` 的结束分片后才保存；提前停止枚举或上游没有返回结束原因时不会保存

保存对话失败会记录错误日志，但不会让已经成功的模型响应失败；请求取消导致的保存取消仍会继续向上抛出

可以在管理端 `/operations/llmconversation` 查询对话记录。记录中保存的是完整渲染内容，设计提示词和传入参数时应考虑这些内容是否适合持久化

## 错误和调用边界

常见错误包括：

- `Code` 不存在、应用未启用或已删除
- User 提示词为空
- 必填模板参数缺失
- `ExtraBodyJson` 不是 JSON 对象或与基础字段冲突
- 关联模型未启用、已删除或配置缺失
- Endpoint、ApiKey、ModelId 无效
- 供应商返回非成功 HTTP 状态、无效 JSON 或异常流事件
- 请求超过 120 秒或被调用方取消

当前实现没有内置重试。模型调用会产生外部费用，调用方增加重试前应明确超时、重复请求和供应商计费语义

`LlmInvokeService` 每次调用只构造一条可选 System 消息和一条 User 消息，不接收历史消息，也不提供工具调用、图片或音频输入。如果业务需要多轮会话或多模态输入，应扩展应用层调用模型和基础设施协议模型，不要在 Controller 中绕过应用层临时拼接供应商请求

`LlmInvokeService` 是 Scoped 服务并使用当前 Scoped `DatabaseContext`。不要直接注入 Singleton；后台服务需要通过 `IServiceScopeFactory` 创建作用域后再获取

## Infrastructure/LLM 的职责

普通业务调用不需要直接接触这一层。它主要提供：

- `ILlmClient`：统一普通和流式对话接口
- `ILlmClientFactory`：按运行时模型配置创建协议客户端
- `LlmModelConfig`：Endpoint、ApiKey、ModelId 和协议类型
- `ChatRequest`、`ChatResponse`、`ChatStreamChunk`：统一模型
- Chat Completions、Responses、Anthropic 三种协议适配

需要新增协议时，通常应：

1. 在 `LlmProtocolType` 增加协议值
2. 新增 `ILlmClient` 实现
3. 在 `DynamicLlmClientFactory` 增加创建分支
4. 更新 `LlmModelService.ValidateProtocolType` 和管理端协议选项
5. 验证普通调用、流式调用、错误响应、取消和 ExtraBody 冲突
6. 更新本文档的协议表

## 使用检查清单

- 调用方是否优先使用 `LlmInvokeService`
- LLM 相关业务服务是否放在 `Application.Service.LLM`
- 宿主是否引用该项目并注册 `BatchRegisterServices()`、`AddHttpClient()`、`AddLlmClientFactory()`
- 模型 Endpoint 是否是与协议匹配的完整请求地址
- 模型和应用是否都已启用
- 业务使用的 `Code` 是否与后台配置完全一致
- 参数字典是否使用合适的大小写比较器
- 必填占位符是否全部提供了非空白值
- ExtraBody 是否为 JSON 对象且没有覆盖基础字段
- 是否传递并响应 `CancellationToken`
- 流式调用是否完整消费并正确处理结束、取消和错误
- 是否了解完整提示词和输出会写入对话记录
