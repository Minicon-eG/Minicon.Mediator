namespace Minicon.Mediator;

/// <summary>
/// Defines a handler for a request that streams its responses.
/// </summary>
/// <typeparam name="TRequest">The concrete request type to handle.</typeparam>
/// <typeparam name="TResponse">The element type produced by the handler.</typeparam>
public interface IStreamRequestHandler<in TRequest, TResponse>
	where TRequest : IStreamRequest<TResponse>
{
	/// <summary>
	/// Handles the request and streams the responses.
	/// </summary>
	/// <param name="request">The incoming request.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
