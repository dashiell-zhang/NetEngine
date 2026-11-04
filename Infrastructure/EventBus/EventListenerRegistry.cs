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
    /// 校验监听签名并为目标注册建立独立解析入口
    /// </summary>
    internal EventListenerRegistry(IEnumerable<ServiceDescriptor> services)
    {

        Dictionary<string, List<EventListener>> listeners = [];
        Dictionary<string, Type> eventTypes = [];
        Dictionary<object, HashSet<MethodInfo>> registered = new(ReferenceEqualityComparer.Instance);
        var registrations = services.ToArray();

        foreach (var service in registrations)
        {
            var implementation = service.IsKeyedService ? service.KeyedImplementationType : service.ImplementationType;
            var instance = service.IsKeyedService ? service.KeyedImplementationInstance : service.ImplementationInstance;
            implementation ??= instance?.GetType();
            implementation ??= service.ServiceType;
            Func<IServiceProvider, object>? resolveService = null;

            foreach (var method in implementation.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.GetCustomAttribute<EventSubscribeAttribute>(true) == null)
                {
                    continue;
                }

                if (implementation.IsInterface || implementation.ContainsGenericParameters || !method.IsPublic || method.IsStatic || method.IsGenericMethod || (method.ReturnType != typeof(Task) && method.ReturnType != typeof(ValueTask)))
                {
                    throw new InvalidOperationException($"事件监听方法必须是封闭服务的公开实例方法，返回 Task 或 ValueTask：{implementation}.{method.Name}");
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
                var identity = instance ?? (object)service;
                if (!registered.TryGetValue(identity, out var registeredMethods))
                {
                    registeredMethods = [];
                    registered.Add(identity, registeredMethods);
                }

                if (!registeredMethods.Add(method))
                {
                    continue;
                }

                if (!listeners.TryGetValue(name, out var methods))
                {
                    methods = [];
                    listeners.Add(name, methods);
                }

                var invocationMethod = GetInvocationMethod(implementation, service.ServiceType, method);
                resolveService ??= CreateServiceResolver(registrations, service, instance);
                methods.Add(new EventListener(resolveService, invocationMethod, eventType, parameters.Length == 2));
            }
        }

        Listeners = listeners;

    }


    /// <summary>
    /// 通过唯一的服务标识或外部实例解析监听，保留原生 DI 行为
    /// </summary>
    private static Func<IServiceProvider, object> CreateServiceResolver(ServiceDescriptor[] registrations, ServiceDescriptor registration, object? instance)
    {

        if (registration.IsKeyedService && Equals(registration.ServiceKey, KeyedService.AnyKey))
        {
            throw new InvalidOperationException($"事件监听服务必须使用具体的 Key，不能使用 AnyKey：{registration.ServiceType}");
        }

        if (instance != null)
        {
            // 外部传入的实例继续由调用方负责释放
            return _ => instance;
        }

        if (registrations.Count(service => service.ServiceType == registration.ServiceType && service.IsKeyedService == registration.IsKeyedService && Equals(service.ServiceKey, registration.ServiceKey)) != 1)
        {
            throw new InvalidOperationException($"事件监听服务标识存在多个注册，请按具体类型或不同 Key 注册：{registration.ServiceType}");
        }

        return provider => registration.IsKeyedService
            ? provider.GetRequiredKeyedService(registration.ServiceType, registration.ServiceKey)
            : provider.GetRequiredService(registration.ServiceType);

    }


    /// <summary>
    /// 选择能接收原事件类型的接口方法，保留逆变注册和显式接口代理的派发
    /// </summary>
    private static MethodInfo GetInvocationMethod(Type implementation, Type serviceType, MethodInfo method)
    {

        if (!serviceType.IsInterface)
        {
            return method;
        }

        var eventType = method.GetParameters()[0].ParameterType;
        var contracts = new[] { serviceType }.Concat(serviceType.GetInterfaces()).ToArray();
        // 逆变注册的接口参数可能更窄，补充同一泛型接口的实际实现契约
        var implementedContracts = implementation.GetInterfaces().Where(candidate => candidate.IsConstructedGenericType
            && contracts.Any(contract => contract.IsConstructedGenericType
                && contract.GetGenericTypeDefinition() == candidate.GetGenericTypeDefinition()
                && contract.IsAssignableFrom(candidate)));

        foreach (var contract in contracts.Concat(implementedContracts).Distinct())
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
                    var interfaceMethod = mapping.InterfaceMethods[index];
                    if (mapping.TargetMethods[index].GetBaseDefinition() == method.GetBaseDefinition() && interfaceMethod.GetParameters()[0].ParameterType.IsAssignableFrom(eventType))
                    {
                        return interfaceMethod;
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
internal sealed record EventListener(Func<IServiceProvider, object> ResolveService, MethodInfo Method, Type EventType, bool HasCancellationToken)
{

    /// <summary>
    /// 在独立异步作用域中解析服务并等待监听函数完成
    /// </summary>
    internal async Task InvokeAsync(IServiceProvider services, string payload, CancellationToken cancellationToken)
    {

        await using var scope = services.CreateAsyncScope();
        var instance = ResolveService(scope.ServiceProvider);
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
