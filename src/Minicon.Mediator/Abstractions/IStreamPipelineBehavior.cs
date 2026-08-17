namespace Minicon.Mediator;

/// <summary>
/// Pipeline behavior intercepting stream handling, similar to a middleware.
/// Behaviors are invoked in registration order and must call <c>next</c>
/// exactly once to continue the pipeline (or short-circuit deliberately).
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The element type of the stream.</typeparam>
public interface IStreamPipelineBehavior<in TRequest, TResponse>
	where TRequest : notnull
{
	/// <summary>
	/// Wraps the inner handler invocation with custom logic.
	/// </summary>
	/// <param name="request">The request being handled.</param>
	/// <param name="next">Delegate that invokes the next behavior or final handler.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
