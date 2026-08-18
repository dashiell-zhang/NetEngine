# WebAPI 过滤器

`ProjectCore/WebAPI.Core/Filters` 提供 WebAPI 宿主共用的 MVC 过滤器，负责异常转换、响应缓存、ETag、请求并发限制、RSA 字段解密和请求签名校验

## 过滤器一览

| 过滤器 | 使用方式 | 主要用途 | 关键依赖 |
|---|---|---|---|
| `ExceptionFilter` | 全局自动注册 | 将 `CustomException` 转换为 400 响应 | 无 |
| `CacheDataFilter` | Controller 或 Action 特性 | 缓存接口返回值并防止缓存击穿 | `IDistributedCache`、`IDistributedLock` |
| `ETagFilter` | Controller 或 Action 特性 | 为 GET 返回值生成 ETag，支持 304 | 无 |
| `QueueLimitFilter` | Controller 或 Action 特性 | 对同一类请求排队或立即阻断 | `IDistributedLock` |
| `RSADecryptFilter` | Controller 或 Action 特性 | 解密请求模型中指定的字符串字段 | `RSA:PrivateKey` 配置 |
| `SignVerifyFilter` | Controller 或 Action 特性 | 校验管理端请求签名 | JWT、请求缓冲中间件 |

除 `ExceptionFilter` 外，其余过滤器本身都是 Attribute，可以直接标记在 Controller 或 Action 上，不需要单独注册过滤器类型

WebAPI 宿主通过 `builder.AddCommonServices()` 注册 MVC 公共服务，通过 `app.UseCommonMiddleware()` 开启请求体重复读取、认证授权和生产环境异常处理中间件。新增宿主使用这些过滤器时，应保持这两个公共入口

## ExceptionFilter

`ExceptionFilter` 已在 `AddCommonServices()` 中全局注册：

```csharp
options.Filters.Add(new ExceptionFilter());
```

业务代码抛出 `CustomException` 时，过滤器会将其转换为：

```http
HTTP/1.1 400 Bad Request
Content-Type: application/json
```

```json
{
  "errMsg": "具体错误信息"
}
```

业务校验失败时，可以直接抛出：

```csharp
throw new CustomException("无效的用户信息");
```

该过滤器只处理 `CustomException`。其他异常不会被标记为已处理；非 Development 环境会继续交给 `UseCommonMiddleware()` 注册的全局异常处理逻辑，Development 环境则保留调试异常行为

不要在 Controller 中重复捕获 `CustomException` 并手工拼装相同响应，业务校验和异常仍应优先发生在 Application 层

## CacheDataFilter

`CacheDataFilter` 用于缓存 Action 的 `ObjectResult.Value`：

```csharp
[HttpGet]
[CacheDataFilter(TTL = 60, IsUseToken = true)]
public Task<UserDto?> GetUser(long userId)
{
    return userService.GetUserAsync(userId);
}
```

### 参数

| 参数 | 默认值 | 说明 |
|---|---:|---|
| `TTL` | `0` | 返回值缓存时长，单位秒；使用时应显式设置为大于 `0` |
| `IsUseToken` | `false` | 是否将完整 `Authorization` 请求头加入缓存键 |

`IsUseToken = true` 适合返回结果与当前登录身份有关的接口。公共数据可以设为 `false`

### 缓存键

当前实现读取以下请求数据：

- Query 参数
- 非文件 Form 参数
- 非 Form 请求的原始 Body
- `IsUseToken = true` 时的 `Authorization` 请求头

序列化后计算 MD5，最终键格式为：

```text
CacheData_<MD5>
```

当前缓存键不包含 Controller、Action、路由，也不包含上传文件内容。不同接口只要参数和 Token 相同，就可能产生相同缓存键。因此新增使用前必须确认不会与其他缓存接口发生键碰撞；如果要把该过滤器作为通用缓存方案，应先扩展实现，将接口标识加入缓存键

原始 JSON Body 和 Query 顺序会影响摘要。同一语义但文本或参数顺序不同的请求，可能生成不同缓存键

### 执行行为

1. 命中缓存时直接返回 `ObjectResult`，不再执行 Action
2. 未命中时尝试获取 60 秒分布式锁
3. 未获得锁的请求每 200 毫秒检查一次缓存
4. 获得锁的请求执行 Action，并缓存非空的 `ObjectResult.Value`
5. Action 完成后释放锁

当前防击穿锁不会自动续期。Action 执行超过 60 秒时，锁可能到期并允许其他请求再次回源

当前写缓存逻辑不检查 HTTP 状态码，只判断结果是否为非空 `ObjectResult`。因此只应标记在稳定的查询接口上，不要用于可能通过 `BadRequestObjectResult` 等对象结果表达业务失败的接口，否则错误结果也可能被缓存，并在命中时以普通 `ObjectResult` 返回

`null`、非 `ObjectResult`、文件结果和流式结果不会写入缓存

### 宿主要求

宿主必须注册 `IDistributedCache`。同时应注册 `IDistributedLock`，用于未命中时的回源互斥。分布式锁的注册和实现选择见 [分布式锁](DistributedLock.md)

## ETagFilter

`ETagFilter` 为成功的 GET 对象结果生成 ETag：

```csharp
[HttpGet]
[ETagFilter]
public Task<ArticleDto?> GetArticle(long id)
{
    return articleService.GetArticleAsync(id);
}
```

过滤器只在以下条件全部满足时生效：

- 请求方法是 GET
- 响应状态码是 200
- Action 结果是 `ObjectResult`
- 返回值不为 `null`

ETag 根据返回值 JSON 的 SHA-256 Base64 摘要生成，并使用双引号包裹。客户端下次请求可以携带：

```http
If-None-Match: "上一次响应中的ETag"
```

完全匹配时返回 `304 Not Modified`，否则在响应头写入新的 `ETag`

该过滤器在 Action 执行完成后计算 ETag，不会跳过数据库查询或业务执行。它用于减少响应体传输，不等同于服务端结果缓存

过滤器执行时 MVC 结果可能尚未真正写入响应，因此只建议用于正常返回 200 对象结果的查询 Action，不要用于通过不同 `ObjectResult` 状态码表达多种结果的接口

当前实现只处理完整的 ETag 精确匹配，没有专门处理弱 ETag 或复杂的多值条件请求

## QueueLimitFilter

`QueueLimitFilter` 使用分布式锁控制同一类请求的并发或调用频率：

```csharp
[HttpPost]
[QueueLimitFilter(IsUseToken = true, IsUseParameter = true, IsBlock = true)]
public Task<bool> UpdateUser(EditUserDto request)
{
    return userService.UpdateUserAsync(request);
}
```

### 参数

| 参数 | 默认值 | 说明 |
|---|---:|---|
| `IsUseParameter` | `false` | 是否将 Query、非文件 Form 或原始 Body 加入锁键 |
| `IsUseToken` | `false` | 是否将完整 `Authorization` 请求头加入锁键 |
| `IsBlock` | `false` | 获取失败时是否立即返回 400；为 `false` 时每 200 毫秒继续尝试 |
| `Expiry` | `0` | 锁的固定保留时间；小于等于 `0` 时使用随 Action 完成释放的 60 秒租约 |

锁键始终包含 `ActionDescriptor.DisplayName`，再根据配置追加 Token 和请求参数，最终格式为：

```text
QueueLimit_<MD5>
```

`IsUseParameter` 使用的是原始 HTTP 参数，不包含路由值和上传文件内容

### 排队与阻断

`IsBlock = true` 时，如果同类请求已经持有锁，接口立即返回：

```http
HTTP/1.1 400 Bad Request
```

```json
{
  "errMsg": "请勿频繁操作"
}
```

`IsBlock = false` 时，请求每 200 毫秒尝试一次，直到获取成功或客户端取消请求。当前实现没有额外的总等待超时

### Expiry 的两种语义

`Expiry <= 0`：

- 锁租约使用 60 秒
- Action 完成后主动释放
- 适合防止同一业务操作并发执行

`Expiry > 0`：

- 锁租约使用指定秒数
- Action 完成后不会主动释放，等待租约自然到期
- 适合在一段固定时间内限制重复调用

因此 `Expiry` 不只是“最长执行时间”，设置为正数会把过滤器从执行期互斥变为固定冷却窗口

当前实现不会自动续期。执行时间超过租约时，新的请求可能在原 Action 尚未完成时获得锁

分布式锁获取发生异常时，过滤器会记录日志并继续执行 Action，属于 fail-open 行为。对于必须严格防重的业务，除使用过滤器外，仍应在 Application 层通过唯一约束、事务或幂等设计保证正确性

## RSADecryptFilter

`RSADecryptFilter` 会遍历请求模型，把标记了 `[RSAEncrypted]` 的字符串属性替换为解密后的明文

请求模型：

```csharp
public class LoginRequest
{
    public string UserName { get; set; } = string.Empty;

    [RSAEncrypted]
    public string Password { get; set; } = string.Empty;
}
```

Action：

```csharp
[HttpPost]
[RSADecryptFilter]
public Task<string?> Login(LoginRequest request)
{
    return authorizeService.LoginAsync(request);
}
```

客户端应使用配置中公钥对应的 RSA 公钥，以 OAEP-SHA256 填充方式加密字符串，并把密文编码为 Base64。服务端从以下配置读取私钥：

```json
{
  "RSA": {
    "PublicKey": "",
    "PrivateKey": ""
  }
}
```

过滤器支持遍历普通嵌套对象，以及运行时类型名称为 `Dictionary<TKey, TValue>`、`List<T>` 和数组的集合。属性需要可读取，带 `[RSAEncrypted]` 的字符串属性还需要可写

仅 Action 的请求绑定参数会被处理。不要把 `[RSAEncrypted]` 标记在响应模型或非字符串属性上

私钥缺失时会抛出异常。密文无效时，Debug 构建只记录日志并保留原值，非 Debug 构建会抛出异常。当前仓库尚未在 Controller 中实际使用该过滤器，首次接入时应同时验证客户端加密方式、嵌套模型和异常响应

## SignVerifyFilter

`SignVerifyFilter` 用于校验客户端是否使用当前 JWT 和请求原文生成了正确签名。Admin.WebAPI 的多数受保护 Controller 已在类级别使用：

```csharp
[SignVerifyFilter]
[Authorize]
[ApiController]
public class UserController : ControllerBase
{
}
```

如果 Controller 已经标记过滤器，可以在单个 Action 上跳过：

```csharp
[SignVerifyFilter(IsSkip = true)]
[HttpGet]
public Task<StatusDto> GetStatusWithoutSignature()
{
    return service.GetStatusAsync();
}
```

当前实现从同类型过滤器中取最后一个配置，用于让 Action 级 `IsSkip` 覆盖 Controller 级设置

`IsSkip` 只跳过请求签名校验，不会跳过 Controller 上的 `[Authorize]`。如果接口还需要匿名访问，应单独按 ASP.NET Core 规则使用 `[AllowAnonymous]`

### 启用范围

签名校验代码只在非 Debug 构建中执行。Debug 构建即使标记 `[SignVerifyFilter]` 也不会验证请求签名

过滤器依赖有效的 Bearer JWT，因为签名原文使用 JWT 的第三段签名值。通常应与 `[Authorize]` 一起使用

### 请求头

客户端需要发送：

```http
Authorization: Bearer <JWT>
Time: <Unix毫秒时间戳>
Token: <请求签名>
```

普通 JSON 请求的签名原文为：

```text
JWT第三段 + Time原始文本 + Request.Path + Request.QueryString + 原始Body
```

`Token` 是上述 UTF-8 文本的 SHA-256 大写十六进制摘要

Form 请求不直接拼接原始 Body，而是：

1. Form 字段按 Key 排序后依次拼接 `Key + Value`
2. 文件按字段名排序
3. 每个文件拼接 `字段名 + 文件内容SHA256大写十六进制摘要`

请求路径、Query 顺序、Body 文本、字段名称和文件内容必须与服务端看到的内容完全一致，否则签名不匹配

仓库内已有客户端实现可以直接参考：

- 普通管理端请求：`Presentation/Admin.App/Libraries/HttpInterceptor.cs`
- 管理端文件上传：`Presentation/Admin.App/Services/UploadSignatureService.cs`
- 服务间请求：`Presentation/Client.WebAPI/Libraries/HttpHandler/HttpSignHandler.cs`

### 校验结果和当前边界

- 签名不一致：返回 401 和 `{ "errMsg": "非法 Token" }`
- 时间戳早于服务器当前时间约 3 分钟以上：返回 401 和 `{ "errMsg": "Token 已过期" }`
- 当前实现没有单独拒绝未来时间戳
- `Time` 缺失或不是合法 Unix 毫秒值时会抛出格式异常，而不是直接返回统一的 401

`UseCommonMiddleware()` 中的请求缓冲必须保留，否则过滤器和其他组件无法安全地重复读取 Body

## 组合使用注意事项

- `[Authorize]` 由认证授权中间件处理，发生在 MVC Action 过滤器之前
- `RSADecryptFilter` 修改的是已经绑定的 Action 参数；签名、缓存键和队列锁键读取的仍是原始 HTTP 请求内容
- `CacheDataFilter` 命中后不会执行其内部的 Action 调用链，组合其他 Action 过滤器时需要实际验证执行顺序
- `CacheDataFilter` 和 `ETagFilter` 都会处理响应，除非已经验证预期行为，否则不要直接叠加
- 防重复提交优先使用 `QueueLimitFilter`，方法内部复杂锁范围使用 `IDistributedLock`
- 过滤器只负责 Web 表现层行为，核心权限、幂等和业务校验仍应放在 Application 或 Repository 层

## 新增或修改过滤器检查清单

- 是否确实属于 HTTP/MVC 表现层职责
- 是否检查了 Controller、Application Service 和请求 DTO 的完整调用链
- 是否明确 Controller 级和 Action 级覆盖关系
- 所需缓存、分布式锁、JWT、RSA 配置是否已经注册
- 是否保持 `AddCommonServices()` 和 `UseCommonMiddleware()` 调用
- 是否验证 Debug 与非 Debug 构建差异
- 是否检查成功、业务失败、系统异常和请求取消路径
- 是否确认缓存键或锁键包含足够的接口、用户和参数维度
- 是否避免把文件内容、Token 或解密后的敏感字段写入日志
