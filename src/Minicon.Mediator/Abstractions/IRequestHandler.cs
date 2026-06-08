namespace Minicon.Mediator;

/// <summary>
/// Defines a handler for a request that returns a response.
/// </summary>
/// <typeparam name="TRequest">The concrete request type to handle.</typeparam>
/// <typeparam name="TResponse">The type of response produced by the handler.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
	where TRequest : IRequest<TResponse>
{
	/// <summary>
	/// Handles the request and returns the response.
	/// </summary>
	/// <param name="request">The incoming request.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Convenience handler interface for requests with no response.
/// Returns <see cref="Unit"/> internally.
/// </summary>
/// <typeparam name="TRequest">The concrete request type to handle.</typeparam>
public interface IRequestHandler<in TRequest> : IRequestHandler<TRequest, Unit>
	where TRequest : IRequest<Unit>
{
}
