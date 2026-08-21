namespace Minicon.Mediator;

/// <summary>
/// Defines a handler for a request that returns a response.
/// </summary>
/// <remarks>
/// <see cref="Handle"/> returns a <see cref="ValueTask{TResult}"/>, matching <c>Mediator</c>
/// (martinothamar). Handlers written against MediatR return <see cref="Task{TResult}"/> — those
/// implement <see cref="ITaskRequestHandler{TRequest, TResponse}"/> instead, which bridges to this
/// interface. C# cannot overload on return type alone, so the two forms cannot share one interface.
/// </remarks>
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
	ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
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
