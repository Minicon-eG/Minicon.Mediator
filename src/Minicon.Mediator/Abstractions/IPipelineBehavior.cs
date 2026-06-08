namespace Minicon.Mediator;

/// <summary>
/// Pipeline behavior intercepting request handling, similar to a middleware.
/// Behaviors are invoked in registration order and must call <paramref name="next"/>
/// exactly once to continue the pipeline (or short-circuit deliberately).
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
	where TRequest : notnull
{
	/// <summary>
	/// Wraps the inner handler invocation with custom logic.
	/// </summary>
	/// <param name="request">The request being handled.</param>
	/// <param name="next">Delegate that invokes the next behavior or final handler.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
