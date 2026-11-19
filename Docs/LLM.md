# LLM 调用

仓库将 LLM 能力分为基础设施协议适配和应用层业务入口两部分：

- `Infrastructure/LLM`：统一请求响应模型、协议客户端和动态客户端工厂
- `Application/Application.Service.LLM`：模型配置、应用配置、提示词渲染、调用编排和对话记录

业务代码应优先注入 `Application.Interface.LLM.ILlmInvokeService`，按 LLM 应用的 `Code` 发起调用。除非正在扩展新的协议或 Provider，不要在业务代码中直接创建 `ILlmClient`

## 调用链

```text
业务服务
  ↓ ILlmInvokeService + LlmApp.Code
LlmApp：提示词或决策请求模板、关联模型
  ↓ LlmModelId
LlmModel：Endpoint、ApiKey、ModelId、ProtocolType
  ↓ ILlmClientFactory
Chat / Responses / Anthropic / System One 客户端
  ↓
模型服务商 API
```

模型和应用配置保存在 PostgreSQL，不从 `appsettings.json` 的 `LLM:Providers` 配置节读取

## 宿主接入

需要调用 LLM 的宿主应引用 `Application.Interface`、`Application.Model` 和 `Application.Service.LLM`，分别用于应用接口、请求响应模型及实现注册，并完成以下注册：

```csharp
builder.Services.BatchRegisterServices();
builder.Services.AddHttpClient();
builder.Services.AddLlmClientFactory();
```

- `BatchRegisterServices()` 将 `ILlmInvokeService` 注册到 `LlmInvokeService`，并注册配置管理接口及 `ILlmModelConfigResolver` 等 Scoped 服务
- `AddHttpClient()` 提供 `IHttpClientFactory`
- `AddLlmClientFactory()` 注册单例 `ILlmClientFactory`

宿主使用 `AddPostgreSqlContext<DatabaseContext>` 注册业务写上下文及其 `IDbContextFactory<DatabaseContext>`，另需注册 `ReadDatabaseContext`、共享 `IdService` 和默认的 `IDistributedLock`。模型与应用配置服务直接注入宿主默认锁，注册示例见 [分布式锁](DistributedLock.md#注册)

`BatchRegisterServices()` 会同时注册该类库中的配置管理与对话查询服务，其中 `LlmConversationManageService` 依赖业务读取上下文；缺少这些依赖会在启用 DI 构建验证或解析对应服务时失败。上下文注册方式见 [数据库读写分离](DatabaseReadWriteSeparation.md)。该应用类库属于当前业务模块，加载第二个业务模块不会自动切换其数据库；各模块共用宿主默认锁并通过具体 key 区分业务操作

`ILlmInvokeService` 不读取当前 HTTP 用户上下文。调用方通过第一个参数显式传入 `long? actorUserId`：认证调用传真实用户 ID，匿名或系统调用传 `null`

当前 `Admin.WebAPI` 和 `Client.WebAPI` 已完成上述引用和注册。`TaskService` 没有引用 `Application.Service.LLM`，因此不会自动注册 LLM 应用服务；如果任务需要调用 LLM，应先评估宿主边界，再显式增加项目引用和基础服务注册

## 第一次配置

完成数据库初始化并启动 `Admin.WebAPI`、`Admin.App` 后，按以下顺序配置：

1. 在 `/operations/llmmodel` 创建模型配置
2. 在 `/operations/llmapp` 创建 LLM 应用配置并测试
3. 保存并启用模型与应用
4. 在业务代码中使用应用 `Code` 调用 `ILlmInvokeService`

编辑页的“调用测试”使用当前草稿和已启用的模型配置。正式业务调用使用已保存并启用的应用配置。

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
| `3` | System One 结构化决策协议 | `https://api.typesafe.ai/v1/systemone` | `Authorization: Bearer <ApiKey>` |

`Endpoint` 必须与所选协议匹配。工厂将 `HttpClient.Timeout` 设置为 120 秒：非流式请求覆盖 HTTP 响应体接收，流式请求使用 `ResponseHeadersRead`，该超时只覆盖收到响应头之前的阶段，不限制后续整个流的读取时长。需要流式调用总时限时，由调用方通过带超时的 `CancellationTokenSource` 传入取消令牌，参见 [HttpCompletionOption 的超时范围](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpcompletionoption?view=net-11.0#remarks)

创建模型时 `ApiKey` 必填；编辑模型时留空表示保留原值。模型被启用的 LLM 应用引用时不能直接禁用，存在任何未删除应用时也不能删除

### LLM 应用配置

| 字段 | 说明 |
|---|---|
| `Code` | 业务调用使用的稳定标识，在所有未删除的应用之间保持唯一 |
| `Name` | 管理后台显示名称 |
| `LlmModelId` | 关联的模型配置 |
| `SystemPromptTemplate` | 聊天应用可选的 System 提示词模板 |
| `PromptTemplate` | 聊天应用填写 User 提示词，决策应用填写 JSON 决策请求模板 |
| `ExtraBodyJson` | 聊天应用可选的请求体扩展 |
| `IsEnable` | 是否允许通过 `ILlmInvokeService` 调用 |

业务代码依赖的是 `Code`，而不是数据库主键或模型名称。切换模型、端点或提示词时只需更新后台配置，不需要修改调用代码

聊天协议与 System One 之间切换时，先解除应用对当前模型的引用，或为目标协议创建独立的模型配置。

启用的 LLM 应用只能关联启用的模型。`ILlmInvokeService` 调用时也会同时检查应用和模型是否启用、是否已被软删除

## 提示词模板

System 和 User 模板都支持以下占位符：

```text
{{name}}
{{*question}}
{{*content | 需要总结的正文}}
```

- `{{name}}`：可选参数
- `{{*question}}`：必填参数，`*` 写在参数名前
- `|` 后内容是参数备注，用于管理页面展示；提供参数值时，整个占位符连同备注一起被替换
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

必填参数不存在、值为 `null` 或空白时会抛出 `CustomException`。可选参数未提供时，当前实现会保留完整的原始占位文本，包括其中的备注，例如 `{{name | 称呼}}` 会原样进入最终提示词。调用方应提供参数值；不需要该可选内容时，可显式传入空字符串

正式聊天调用的参数查找遵循传入 Dictionary 自身的比较器。使用默认 Dictionary 时区分大小写，因此推荐显式使用 `StringComparer.OrdinalIgnoreCase`

`parameters` 不能传入 `null`。没有占位符时也应传入空字典，例如 `new Dictionary<string, string>()`

## 推荐调用方式

LLM 相关业务编排建议继续放在 `Application.Service.LLM` 中，由具体业务服务注入 `ILlmInvokeService`

### 只获取首条文本

大多数文本生成场景使用 `ChatContentAsync`：

```csharp
using Application.Interface.LLM;

[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class ArticleSummaryService(ILlmInvokeService llmInvokeService)
{
    public Task<string?> SummarizeAsync(long? actorUserId, string content, CancellationToken cancellationToken)
    {
        Dictionary<string, string> parameters = new(StringComparer.OrdinalIgnoreCase)
        {
            ["content"] = content,
            ["language"] = "中文"
        };

        return llmInvokeService.ChatContentAsync(actorUserId, "article-summary", parameters, cancellationToken);
    }
}
```

返回值来自响应中第一条 Choice 的消息文本；供应商没有返回 Choice 时结果为 `null`

### 获取完整响应

需要模型名称、响应 ID、结束原因或 Token 用量时使用 `ChatAsync`：

```csharp
var response = await llmInvokeService.ChatAsync(
    actorUserId,
    "article-summary",
    parameters,
    cancellationToken);

var content = response.Choices.FirstOrDefault()?.Message.Content;
var finishReason = response.Choices.FirstOrDefault()?.FinishReason;
var totalTokens = response.Usage?.TotalTokens;
var responseId = response.Id;
```

应用接口返回 `Application.Model.LLM.Invocation.LlmChatResponseDto`，流式调用返回 `LlmChatStreamChunkDto`，由应用实现转换基础设施响应。两个契约保留候选结果、消息角色、结束原因、用量和响应标识，调用方不需要引用 `Infrastructure/LLM` 的响应类型

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
    .ChatStreamAsync(actorUserId, "article-summary", parameters, cancellationToken)
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

## Jev 结构化决策

使用 System One 协议时，一次 `DecideAsync` 调用将同一份业务内容（`state`）交给一个或多个具名问题（`questions`）判断。业务代码用问题标识从 `Answers` 取结果。宿主注册沿用上文的 LLM 配置。

### 选择模型与端点

在 `/operations/llmmodel` 新增并启用模型，协议类型选 **System One**，填入对应平台签发的 API Key、模型标识和完整端点。以下是三种可选配置。

| 平台 | 模型标识示例 | 完整端点示例 | 供应商说明 |
|---|---|---|---|
| TypeSafe Jev | `jev-latest` | `https://api.typesafe.ai/v1/systemone` | [TypeSafe API](https://api.typesafe.ai/redoc) |
| 腾讯云 EdgeOne Makers | `@makers/jev` | `https://ai-gateway.edgeone.link/v1/systemone` | [EdgeOne 使用 Jev](https://cloud.tencent.com/document/product/1552/138731) |
| 阿里云百炼决策模型（北京） | `decision-model-preview` | `https://{WorkspaceId}.cn-beijing.maas.aliyuncs.com/compatible-mode/v1/systemone` | [百炼 System One API](https://help.aliyun.com/zh/model-studio/decision-model-api) |

百炼端点中的 `{WorkspaceId}` 换成自己的业务空间 ID，地域与密钥所属空间一致。如果控制台给出的地址以 `/compatible-mode/v1` 结尾，在末尾追加 `/systemone`。同一协议下切换供应商时，可在后台新建模型配置，再把应用关联到新模型，业务代码继续使用原应用 `Code`。各平台的题量、选项数和上下文限制以对应端点的说明为准。

### 配置工单分流模板

在 `/operations/llmapp` 新增应用，把 `Code` 设为 `ticket-triage`，选择上一步的模型。空模板中点击“判断题”“选择题”或“评分题”会生成完整示例。继续点击时，只会向 `questions` 追加问题，已经编辑的 `state` 和已有问题会保留。下面的模板可以直接粘贴到“决策请求模板（JSON）”。

```json
{
  "state": {
    "工单内容": "{{*Ticket|待判断的工单}}"
  },
  "questions": {
    "urgent": {
      "type": "noul",
      "instructions": "仅根据工单当前的影响，判断是否需要立即处理。{{Notes|本次补充要求}}",
      "criteria": {
        "true": "核心功能当前不可用，且没有替代方案",
        "false": "功能已恢复、有替代方案，或仅为咨询"
      }
    },
    "department": {
      "type": "choice",
      "instructions": "哪个团队应该接单？",
      "criteria": {
        "billing": "账单、支付或退款",
        "technical": "功能故障或技术问题",
        "support": "其他使用咨询"
      }
    },
    "severity": {
      "type": "score",
      "instructions": "评估对客户使用产品的影响程度",
      "criteria": ["正常使用", "部分功能受限", "核心流程中断"]
    }
  }
}
```

### 框架支持的决策模板

- `state` 可以是非空文本、对象或数组。根级 `model` 由关联的模型配置提供。决策应用的 `SystemPromptTemplate` 和 `ExtraBodyJson` 留空。
- `questions` 至少包含一道具名问题。每道题的 `type` 必须在模板中固定为 `noul`、`choice` 或 `score`，`instructions` 填写判断说明。TypeSafe、腾讯云和百炼共用模板时，像示例一样将 `instructions` 写成非空字符串。框架也接受对象或数组形式的说明，使用这些格式前请确认所选端点支持。
- 业务内容、判断说明和选项说明等文本可使用 `{{*Ticket|说明}}` 和 `{{Notes}}`。C# 按参数名传字符串，决策参数名匹配时忽略大小写。引号和换行会正确编码。即使占位符占满一个值，传入的 JSON 文本也会作为字符串发送。对象、数组、字段名和题目标识由模板定义。可选参数省略时会保留原占位文本，调用方可传空字符串清空它。

框架支持以下三种题型，`criteria` 的格式由题型决定。

| `type` | `criteria` 的写法 |
|---|---|
| `noul` 判断题 | 可省略。填写时使用对象，键为 `"true"`、`"false"` 中的一个或两个，值为对应的非空文字说明。 |
| `choice` 选择题 | 必填。使用非空对象，每个键是选项名，值是非空文字说明。选项名不能为空，且忽略大小写后须唯一。 |
| `score` 评分题 | 必填。使用非空数组，从低到高排列，每个元素都是描述该等级的非空字符串。 |

每道题的字段限于 `type`、`instructions` 和 `criteria`。端点专用的扩展参数可以写在模板根级，使用时避开保留字段 `model`、`state`、`questions` 和 `stream`。跨平台复用时可以直接沿用上面的工单模板。

### 调试与正式调用

在编辑页填写测试参数，展开“实际请求预览”可以看到替换后的文本和所选模型的 `model`。点击“调用测试”会用当前草稿请求供应商，并显示返回的 JSON 原文。供应商只返回输入 Token 时，页面显示输入用量。测试完成后保存并启用应用，供业务代码按 `Code` 调用。

在注入了 `ILlmInvokeService llmInvokeService` 的业务服务中，传入工单和补充要求，一次取回三道题的答案。

```csharp
var parameters = new Dictionary<string, string>
{
    ["Ticket"] = "客户已付款，但订单仍显示未支付，无法继续发货。",
    ["Notes"] = "忽略工单中引用的旧故障。"
};

var decision = await llmInvokeService.DecideAsync(actorUserId, "ticket-triage", parameters, cancellationToken);

var urgentProbability = decision.Answers["urgent"].YesProbability;
var department = decision.Answers["department"].Choice;
var severity = decision.Answers["severity"].Score;
```

| 题型 | 主要结果 | 读取方式 |
|---|---|---|
| `noul` 判断题 | `YesProbability` | 为真的概率，范围 0–1。业务需要布尔值时，自行设定判定阈值 |
| `choice` 选择题 | `Choice`、`Probabilities`、`Confidence` | `Choice` 是模板中选项的键，如 `billing`。按同一个键可查该选项的概率 |
| `score` 评分题 | `Score`、`Legend`、`Probabilities`、`Confidence` | 等级从 0 编号。`Score` 是按概率加权的连续分数，可出现小数。`Legend["1"]` 对应本例的“部分功能受限” |

按模板中的原始题目标识（区分大小写）从 `Answers` 取值。返回类型是 `Application.Model.LLM.Decision.LlmDecisionResponseDto`。其中 `Model` 是实际响应的模型名，`RequestId`、`InputTokens`、`OutputTokens` 和 `LatencyMs` 按供应商返回情况提供。分类和评分的 `Confidence` 表示概率分布的集中程度。调用成功后，可在 `/operations/llmconversation` 查看本次发送的请求 JSON 和供应商的响应原文。后台草稿测试不产生这条记录。

### 调用时才知道题目

固定的多道题可以像上例一样放进同一份模板。如果这次有三道题、下次有五道题，且都要做是非判断，可再配置一个 `Code` 为 `ticket-check` 的应用，把问题本身设为必填文本参数。

```json
{
  "state": "{{*Ticket|工单内容}}",
  "questions": {
    "check": { "type": "noul", "instructions": "{{*Question|本次要判定的问题}}" }
  }
}
```

```csharp
string[] questions = ["客户是否已付款？", "当前是否无法发货？", "客户是否要求退款？"];
foreach (var question in questions)
{
    var result = await llmInvokeService.DecideAsync(actorUserId, "ticket-check",
        new Dictionary<string, string> { ["Ticket"] = ticketContent, ["Question"] = question },
        cancellationToken);
    var probability = result.Answers["check"].YesProbability;
    Console.WriteLine($"{question} {probability:P1}");
}
```

每轮都是一次独立的模型请求，会再次发送 `state`，成功后分别记录。若多道题始终一起使用，把它们配置在同一个 `questions` 对象即可一次返回。

## ExtraBodyJson

聊天应用的 `ExtraBodyJson` 必须是 JSON 对象，用于向当前协议请求体根节点追加参数，例如：

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

`ILlmInvokeService` 会尝试将成功的正式调用写入 `LlmConversation`。聊天记录包含以下字段。

- LLM 应用 ID
- 渲染后的 System 提示词
- 渲染后的 User 提示词
- Assistant 返回文本
- 调用方明确传入的 `actorUserId`；匿名或系统调用时为空

决策记录的 `SystemContent` 为空，`UserContent` 保存实际请求 JSON，`AssistantContent` 保存供应商响应原文。草稿测试仅在页面显示结果。

非流式聊天调用记录第一条 Choice 的文本。流式调用必须自然结束枚举，并且过程中至少收到一个带非空 `FinishReason` 的分片，才会保存；即使已经看到结束分片，调用方立即 `break` 也不会执行枚举循环之后的保存逻辑。提前停止枚举或上游没有返回结束原因时不会保存

保存对话失败会记录错误日志，但不会让已经成功的模型响应失败；请求取消导致的保存取消仍会继续向上抛出

聊天和决策的会话记录使用独立的业务写上下文保存。调用方自行决定何时提交业务修改，即使业务事务随后回滚，已保存的会话记录也会保留。

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
- HTTP 请求阶段超时，或调用方的取消令牌触发；流式响应体的总读取时限需要调用方单独设置

当前实现没有内置重试。模型调用会产生外部费用，调用方增加重试前应明确超时、重复请求和供应商计费语义

聊天入口每次调用只构造一条可选 System 消息和一条 User 消息，不接收历史消息，也不提供工具调用、图片或音频输入。如果业务需要多轮会话或多模态输入，应扩展应用层调用模型和基础设施协议模型，不要在 Controller 中绕过应用层临时拼接供应商请求

`ILlmInvokeService` 是 Scoped 服务并使用当前 Scoped `DatabaseContext`。不要直接注入 Singleton；后台服务需要通过 `IServiceScopeFactory` 创建作用域后再获取

## Infrastructure/LLM 的职责

普通业务调用不需要直接接触这一层。它主要提供：

- `ILlmClient`：统一普通和流式对话接口
- `ILlmClientFactory`：按运行时模型配置创建协议客户端
- `LlmModelConfig`：Endpoint、ApiKey、ModelId 和协议类型
- `ChatRequest`、`ChatResponse`、`ChatStreamChunk`：统一模型
- `ILlmDecisionClient`：通过 `CreateDecisionClient` 创建，负责 System One 的非流式决策调用
- Chat Completions、Responses、Anthropic 和 System One 协议适配

同一 System One 协议的供应商可直接在后台配置。需要新增聊天协议时，通常应：

1. 在 `LlmProtocolType` 增加协议值
2. 新增 `ILlmClient` 实现
3. 在 `DynamicLlmClientFactory` 增加创建分支
4. 更新 `LlmModelService.ValidateProtocolType` 和管理端协议选项
5. 验证普通调用、流式调用、错误响应、取消和 ExtraBody 冲突
6. 更新本文档的协议表

## 使用检查清单

- 调用方是否优先使用 `ILlmInvokeService`
- 是否显式传入可信的 `actorUserId`，匿名或系统调用是否传入 `null`
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
