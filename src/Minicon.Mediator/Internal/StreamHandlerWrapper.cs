namespace Minicon.Mediator.Internal;

/// <summary>
/// Untyped base used by <see cref="Mediator.CreateStream(object, CancellationToken)"/>.
/// Concrete wrappers downcast at the call site once and dispatch to a strongly-typed handler.
/// </summary>
internal abstract class StreamHandlerWrapper
{
	public abstract IAsyncEnumerable<object?> Handle(object request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

/// <summary>
/// Strongly-typed wrapper. The mediator caches one instance per concrete request type and
/// invokes <see cref="Handle"/> directly without any boxing of the streamed elements.
/// </summary>
internal abstract class StreamHandlerWrapper<TResponse> : StreamHandlerWrapper
{
	public abstract IAsyncEnumerable<TResponse> Handle(IStreamRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
