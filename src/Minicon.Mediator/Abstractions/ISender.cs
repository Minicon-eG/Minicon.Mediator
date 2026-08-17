namespace Minicon.Mediator;

/// <summary>
/// Sends requests to a single handler.
/// </summary>
public interface ISender
{
	/// <summary>
	/// Sends a request to its handler and returns the response.
	/// </summary>
	Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Sends a void-returning request to its handler.
	/// </summary>
	Task<Unit> Send(IRequest request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Sends a request whose runtime type is not known statically.
	/// </summary>
	Task<object?> Send(object request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Sends a request to its handler and streams the responses as they are produced.
	/// The handler is resolved eagerly; elements are pulled lazily during enumeration.
	/// </summary>
	IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Streams a request whose runtime type is not known statically.
	/// </summary>
	IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default);
}
