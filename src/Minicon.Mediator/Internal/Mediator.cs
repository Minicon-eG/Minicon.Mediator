using System.Collections.Concurrent;
using System.Reflection;

namespace Minicon.Mediator.Internal;

/// <summary>
/// High-performance mediator implementation.
///
/// Performance characteristics vs. MediatR v14:
/// <list type="bullet">
///   <item>Type-keyed wrapper cache uses <see cref="ConcurrentDictionary{TKey,TValue}"/> with a static
///         factory method to avoid closure allocations on cache misses.</item>
///   <item>Strongly-typed <c>RequestHandlerWrapper&lt;TResponse&gt;</c> avoids the
///         <c>Task.ContinueWith</c> response cast that MediatR uses for its object-typed pipeline.</item>
///   <item>Pipeline construction walks behaviors via recursive index dispatch rather than
///         <c>.Reverse().Aggregate()</c>; allocations scale with behavior count, not with LINQ overhead.</item>
///   <item>Publish takes a single-handler fast path that bypasses exception aggregation entirely.</item>
/// </list>
/// </summary>
public sealed class Mediator : IMediator
{
	private static readonly ConcurrentDictionary<Type, RequestHandlerWrapper> RequestWrapperCache = new();
	private static readonly ConcurrentDictionary<Type, NotificationHandlerWrapper> NotificationWrapperCache = new();
	private static readonly ConcurrentDictionary<Type, StreamHandlerWrapper> StreamWrapperCache = new();

	private readonly IServiceProvider _serviceProvider;

	public Mediator(IServiceProvider serviceProvider)
	{
		ArgumentNullException.ThrowIfNull(serviceProvider);
		_serviceProvider = serviceProvider;
	}

	/// <inheritdoc />
	public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		Type requestType = request.GetType();
		RequestHandlerWrapper untyped = RequestWrapperCache.GetOrAdd(requestType, CreateRequestWrapper);
		RequestHandlerWrapper<TResponse> wrapper = (RequestHandlerWrapper<TResponse>)untyped;
		return wrapper.Handle(request, _serviceProvider, cancellationToken);
	}

	/// <inheritdoc />
	public Task<Unit> Send(IRequest request, CancellationToken cancellationToken = default)
		=> Send<Unit>(request, cancellationToken);

	/// <inheritdoc />
	public Task<object?> Send(object request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (request is not IBaseRequest)
		{
			throw new ArgumentException(
				$"Type '{request.GetType().FullName}' does not implement '{nameof(IBaseRequest)}'.", nameof(request));
		}

		Type requestType = request.GetType();
		RequestHandlerWrapper wrapper = RequestWrapperCache.GetOrAdd(requestType, CreateRequestWrapper);
		return wrapper.Handle(request, _serviceProvider, cancellationToken);
	}

	/// <inheritdoc />
	public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		Type requestType = request.GetType();
		StreamHandlerWrapper untyped = StreamWrapperCache.GetOrAdd(requestType, CreateStreamWrapper);
		StreamHandlerWrapper<TResponse> wrapper = (StreamHandlerWrapper<TResponse>)untyped;
		return wrapper.Handle(request, _serviceProvider, cancellationToken);
	}

	/// <inheritdoc />
	public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (request is not IBaseRequest)
		{
			throw new ArgumentException(
				$"Type '{request.GetType().FullName}' does not implement '{nameof(IBaseRequest)}'.", nameof(request));
		}

		Type requestType = request.GetType();
		StreamHandlerWrapper wrapper = StreamWrapperCache.GetOrAdd(requestType, CreateStreamWrapper);
		return wrapper.Handle(request, _serviceProvider, cancellationToken);
	}

	/// <inheritdoc />
	public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
		where TNotification : INotification
	{
		if (notification is null)
		{
			throw new ArgumentNullException(nameof(notification));
		}

		// We can't bypass the wrapper cache here because the runtime type may be a derived
		// type with additional handlers registered against it.
		Type notificationType = notification.GetType();
		NotificationHandlerWrapper wrapper = NotificationWrapperCache.GetOrAdd(notificationType, CreateNotificationWrapper);
		return wrapper.Handle(notification, _serviceProvider, cancellationToken);
	}

	/// <inheritdoc />
	public Task Publish(object notification, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(notification);

		if (notification is not INotification)
		{
			throw new ArgumentException(
				$"Type '{notification.GetType().FullName}' does not implement '{nameof(INotification)}'.", nameof(notification));
		}

		Type notificationType = notification.GetType();
		NotificationHandlerWrapper wrapper = NotificationWrapperCache.GetOrAdd(notificationType, CreateNotificationWrapper);
		return wrapper.Handle(notification, _serviceProvider, cancellationToken);
	}

	private static RequestHandlerWrapper CreateRequestWrapper(Type requestType)
	{
		Type? requestInterface = null;
		Type[] interfaces = requestType.GetInterfaces();
		for (int i = 0; i < interfaces.Length; i++)
		{
			Type candidate = interfaces[i];
			if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IRequest<>))
			{
				requestInterface = candidate;
				break;
			}
		}

		if (requestInterface is null)
		{
			throw new InvalidOperationException(
				$"Type '{requestType.FullName}' does not implement IRequest<T>.");
		}

		Type responseType = requestInterface.GetGenericArguments()[0];
		Type wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, responseType);
		return (RequestHandlerWrapper)Activator.CreateInstance(wrapperType, nonPublic: true)!;
	}

	private static StreamHandlerWrapper CreateStreamWrapper(Type requestType)
	{
		Type? requestInterface = null;
		Type[] interfaces = requestType.GetInterfaces();
		for (int i = 0; i < interfaces.Length; i++)
		{
			Type candidate = interfaces[i];
			if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IStreamRequest<>))
			{
				requestInterface = candidate;
				break;
			}
		}

		if (requestInterface is null)
		{
			throw new InvalidOperationException(
				$"Type '{requestType.FullName}' does not implement IStreamRequest<T>.");
		}

		Type responseType = requestInterface.GetGenericArguments()[0];
		Type wrapperType = typeof(StreamHandlerWrapperImpl<,>).MakeGenericType(requestType, responseType);
		return (StreamHandlerWrapper)Activator.CreateInstance(wrapperType, nonPublic: true)!;
	}

	private static NotificationHandlerWrapper CreateNotificationWrapper(Type notificationType)
	{
		Type wrapperType = typeof(NotificationHandlerWrapperImpl<>).MakeGenericType(notificationType);
		return (NotificationHandlerWrapper)Activator.CreateInstance(wrapperType, nonPublic: true)!;
	}

	// Reserved for diagnostic/test access.
	internal static int CachedRequestWrapperCount => RequestWrapperCache.Count;
	internal static int CachedNotificationWrapperCount => NotificationWrapperCache.Count;
	internal static int CachedStreamWrapperCount => StreamWrapperCache.Count;
	internal static Assembly Assembly => typeof(Mediator).Assembly;
}
