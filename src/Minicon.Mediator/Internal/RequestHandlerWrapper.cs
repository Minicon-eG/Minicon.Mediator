namespace Minicon.Mediator.Internal;

/// <summary>
/// Untyped base used by <see cref="Mediator.Send(object, CancellationToken)"/>.
/// Concrete wrappers downcast at the call site once and dispatch to a strongly-typed handler.
/// </summary>
internal abstract class RequestHandlerWrapper
{
	public abstract Task<object?> Handle(object request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

/// <summary>
/// Strongly-typed wrapper. The mediator caches one instance per concrete request type and
/// invokes <see cref="Handle"/> directly without any boxing of the response.
/// </summary>
internal abstract class RequestHandlerWrapper<TResponse> : RequestHandlerWrapper
{
	public abstract Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
