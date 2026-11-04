using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;

namespace EventBus;

/// <summary>
/// 从已注册服务中建立事件方法目录
/// </summary>
internal sealed class EventListenerRegistry
{

    /// <summary>
    /// 按事件类型名称分组的监听方法
    /// </summary>
    internal IReadOnlyDictionary<string, List<EventListener>> Listeners { get; }


    /// <summary>
    /// 校验监听签名并记录服务解析位置
    /// </summary>
    internal EventListenerRegistry(IEnumerable<ServiceDescriptor> services)
    {

        Dictionary<string, List<EventListener>> listeners = [];
        Dictionary<string, Type> eventTypes = [];
        HashSet<(Type, MethodInfo)> registered = [];
        var registrations = services.ToArray();

        for (int registrationIndex = 0; registrationIndex < registrations.Length; registrationIndex++)
        {
            var service = registrations[registrationIndex];
            var implementation = service.IsKeyedService ? service.KeyedImplementationType : service.ImplementationType;
            implementation ??= service.IsKeyedService ? service.KeyedImplementationInstance?.GetType() : service.ImplementationInstance?.GetType();
            implementation ??= service.ServiceType;

            foreach (var method in implementation.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.GetCustomAttribute<EventSubscribeAttribute>(true) == null)
                {
                    continue;
                }

                if (service.IsKeyedService || implementation.IsInterface || implementation.ContainsGenericParameters || !method.IsPublic || method.IsStatic || method.IsGenericMethod || (method.ReturnType != typeof(Task) && method.ReturnType != typeof(ValueTask)))
                {
                    throw new InvalidOperationException($"事件监听方法必须是非 Keyed 服务的公开实例方法，返回 Task 或 ValueTask：{implementation}.{method.Name}");
                }

                var parameters = method.GetParameters();
                if (parameters.Length is < 1 or > 2 || parameters[0].ParameterType == typeof(CancellationToken) || parameters[0].IsOut || parameters[0].ParameterType.IsByRef || (parameters.Length == 2 && parameters[1].ParameterType != typeof(CancellationToken)))
                {
                    throw new InvalidOperationException($"事件监听方法需要一个事件参数及可选的 CancellationToken：{implementation}.{method.Name}");
                }

                var eventType = parameters[0].ParameterType;
                var name = EventSerialization.GetName(eventType);
                if (eventTypes.TryGetValue(name, out var previousType) && previousType != eventType)
                {
                    throw new InvalidOperationException($"广播事件名称冲突：{name}");
                }

                eventTypes[name] = eventType;
                if (!registered.Add((implementation, method)))
                {
                    continue;
                }

                if (!listeners.TryGetValue(name, out var methods))
                {
                    methods = [];
                    listeners.Add(name, methods);
                }

                var invocationMethod = GetInvocationMethod(implementation, service.ServiceType, method);
                int index = registrations.Take(registrationIndex).Count(t => MatchesService(t, service.ServiceType));
                methods.Add(new EventListener(service.ServiceType, index, invocationMethod, eventType, parameters.Length == 2));
            }
        }

        Listeners = listeners;

    }


    /// <summary>
    /// 按 DI 集合解析规则判断注册是否占据目标服务的位置
    /// </summary>
    private static bool MatchesService(ServiceDescriptor registration, Type serviceType)
    {

        if (registration.IsKeyedService)
        {
            return false;
        }

        if (registration.ServiceType == serviceType)
        {
            return true;
        }

        if (!serviceType.IsConstructedGenericType || registration.ServiceType != serviceType.GetGenericTypeDefinition() || registration.ImplementationType == null)
        {
            return false;
        }

        try
        {
            _ = registration.ImplementationType.MakeGenericType(serviceType.GenericTypeArguments);
            return true;
        }
        catch (ArgumentException)
        {
            // DI 集合解析会跳过不满足目标类型参数约束的开放泛型实现
            return false;
        }

    }


    /// <summary>
    /// 为接口注册保留实际接口派发，避免绕过显式接口代理
    /// </summary>
    private static MethodInfo GetInvocationMethod(Type implementation, Type serviceType, MethodInfo method)
    {

        if (!serviceType.IsInterface)
        {
            return method;
        }

        foreach (var contract in new[] { serviceType }.Concat(serviceType.GetInterfaces()))
        {
            // 代理的接口映射可能指向显式实现，继续向基类查找原监听方法的对应契约
            for (var type = implementation; type != null; type = type.BaseType)
            {
                if (!contract.IsAssignableFrom(type))
                {
                    continue;
                }

                var mapping = type.GetInterfaceMap(contract);
                for (int index = 0; index < mapping.TargetMethods.Length; index++)
                {
                    if (mapping.TargetMethods[index].GetBaseDefinition() == method.GetBaseDefinition())
                    {
                        return mapping.InterfaceMethods[index];
                    }
                }
            }
        }

        return method;

    }

}

/// <summary>
/// 保存监听方法的依赖注入解析和调用信息
/// </summary>
internal sealed record EventListener(Type ServiceType, int ServiceIndex, MethodInfo Method, Type EventType, bool HasCancellationToken)
{

    /// <summary>
    /// 在独立异步作用域中解析服务并等待监听函数完成
    /// </summary>
    internal async Task InvokeAsync(IServiceProvider services, string payload, CancellationToken cancellationToken)
    {

        await using var scope = services.CreateAsyncScope();
        var instance = scope.ServiceProvider.GetServices(ServiceType).ElementAt(ServiceIndex);
        var message = JsonSerializer.Deserialize(payload, EventType, EventSerialization.Options)
            ?? throw new JsonException($"事件参数不能为空：{EventType.FullName}");
        var result = Method.Invoke(instance, HasCancellationToken ? [message, cancellationToken] : [message]);
        if (result is Task task)
        {
            await task.ConfigureAwait(false);
        }
        else if (result is ValueTask valueTask)
        {
            await valueTask.ConfigureAwait(false);
        }
        else
        {
            throw new InvalidOperationException($"事件监听方法没有返回可等待的结果：{Method}");
        }

    }

}
