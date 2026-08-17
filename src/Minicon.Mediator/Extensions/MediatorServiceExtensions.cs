using System.Reflection;
using Minicon.Mediator;
using Minicon.Mediator.Internal;

// Match MediatR's namespace placement so existing call sites (`services.AddMediator(...)`) compile
// without adding new using directives.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI extensions registering the mediator and scanning assemblies for handlers.
/// </summary>
public static class MediatorServiceExtensions
{
	/// <summary>
	/// Registers the mediator and scans configured assemblies for
	/// <see cref="IRequestHandler{TRequest,TResponse}"/>, <see cref="IStreamRequestHandler{TRequest,TResponse}"/>
	/// and <see cref="INotificationHandler{TNotification}"/> implementations.
	/// </summary>
	public static IServiceCollection AddMediator(this IServiceCollection services, Action<MediatorServiceConfiguration> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		MediatorServiceConfiguration configuration = new();
		configure(configuration);

		// Mediator is scoped: the scoped service provider is used to resolve handlers,
		// so request-scoped state in handlers/behaviors works the way callers expect.
		services.TryAddScoped<IMediator, Mediator>();
		services.TryAddScoped<ISender>(sp => sp.GetRequiredService<IMediator>());
		services.TryAddScoped<IPublisher>(sp => sp.GetRequiredService<IMediator>());

		foreach (Assembly assembly in configuration.AssembliesToRegister)
		{
			RegisterHandlersFromAssembly(services, assembly, configuration.Lifetime);
		}

		return services;
	}

	private static void RegisterHandlersFromAssembly(IServiceCollection services, Assembly assembly, ServiceLifetime lifetime)
	{
		Type[] types;
		try
		{
			types = assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			// Tolerate dynamic/missing types in test contexts the same way MediatR does.
			types = Array.FindAll(ex.Types, static t => t is not null)!;
		}

		Type requestHandlerOpen = typeof(IRequestHandler<,>);
		Type notificationHandlerOpen = typeof(INotificationHandler<>);
		Type streamRequestHandlerOpen = typeof(IStreamRequestHandler<,>);

		for (int i = 0; i < types.Length; i++)
		{
			Type type = types[i];
			if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition || !type.IsClass)
			{
				continue;
			}

			Type[] interfaces = type.GetInterfaces();
			for (int j = 0; j < interfaces.Length; j++)
			{
				Type @interface = interfaces[j];
				if (!@interface.IsGenericType)
				{
					continue;
				}

				Type definition = @interface.GetGenericTypeDefinition();
				if (definition == requestHandlerOpen || definition == notificationHandlerOpen || definition == streamRequestHandlerOpen)
				{
					services.Add(new ServiceDescriptor(@interface, type, lifetime));
				}
			}
		}
	}

	private static bool ContainsService<TService>(IServiceCollection services)
		where TService : class
	{
		for (int i = 0; i < services.Count; i++)
		{
			if (services[i].ServiceType == typeof(TService))
			{
				return true;
			}
		}

		return false;
	}

	private static void TryAddScoped<TService, TImplementation>(this IServiceCollection services)
		where TService : class
		where TImplementation : class, TService
	{
		if (!ContainsService<TService>(services))
		{
			services.AddScoped<TService, TImplementation>();
		}
	}

	private static void TryAddScoped<TService>(this IServiceCollection services, Func<IServiceProvider, TService> factory)
		where TService : class
	{
		if (!ContainsService<TService>(services))
		{
			services.AddScoped(factory);
		}
	}
}
