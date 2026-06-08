using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Minicon.Mediator;

/// <summary>
/// Configuration object passed to <c>services.AddMediator(cfg =&gt; ...)</c>. Mirrors the
/// MediatR v12+ public surface used by callers (assembly registration + lifetime selection).
/// </summary>
public sealed class MediatorServiceConfiguration
{
	/// <summary>Assemblies registered for handler scanning.</summary>
	public List<Assembly> AssembliesToRegister { get; } = new();

	/// <summary>
	/// Service lifetime applied to discovered handlers. Defaults to <see cref="ServiceLifetime.Transient"/>
	/// to match MediatR behavior.
	/// </summary>
	public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;

	/// <summary>
	/// Registers handlers from the specified assembly for scanning during DI build.
	/// </summary>
	public MediatorServiceConfiguration RegisterServicesFromAssembly(Assembly assembly)
	{
		ArgumentNullException.ThrowIfNull(assembly);
		if (!AssembliesToRegister.Contains(assembly))
		{
			AssembliesToRegister.Add(assembly);
		}

		return this;
	}

	/// <summary>
	/// Registers handlers from multiple assemblies.
	/// </summary>
	public MediatorServiceConfiguration RegisterServicesFromAssemblies(params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(assemblies);
		foreach (Assembly assembly in assemblies)
		{
			RegisterServicesFromAssembly(assembly);
		}

		return this;
	}

	/// <summary>
	/// Registers handlers from the assembly that contains the supplied marker types.
	/// </summary>
	public MediatorServiceConfiguration RegisterServicesFromAssemblyContaining(params Type[] markerTypes)
	{
		ArgumentNullException.ThrowIfNull(markerTypes);
		foreach (Type type in markerTypes)
		{
			RegisterServicesFromAssembly(type.Assembly);
		}

		return this;
	}

	/// <summary>
	/// Registers handlers from the assembly that contains <typeparamref name="T"/>.
	/// </summary>
	public MediatorServiceConfiguration RegisterServicesFromAssemblyContaining<T>()
		=> RegisterServicesFromAssembly(typeof(T).Assembly);
}
