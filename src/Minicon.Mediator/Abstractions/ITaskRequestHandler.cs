namespace Minicon.Mediator;

/// <summary>
/// Defines a handler for a request that returns a response as a <see cref="Task{TResult}"/> — the shape
/// MediatR handlers are written in. Implement this instead of
/// <see cref="IRequestHandler{TRequest, TResponse}"/> to keep a <c>Task</c>-returning <c>Handle</c>.
/// </summary>
/// <remarks>
/// The handler is resolved through <see cref="IRequestHandler{TRequest, TResponse}"/>, which this
/// interface satisfies by wrapping the returned <see cref="Task{TResult}"/> in a
/// <see cref="ValueTask{TResult}"/> — no extra allocation for that step.
/// </remarks>
/// <typeparam name="TRequest">The concrete request type to handle.</typeparam>
/// <typeparam name="TResponse">The type of response produced by the handler.</typeparam>
public interface ITaskRequestHandler<in TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
	where TRequest : IRequest<TResponse>
{
	/// <summary>
	/// Handles the request and returns the response.
	/// </summary>
	/// <param name="request">The incoming request.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	new Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);

	/// <inheritdoc />
	ValueTask<TResponse> IRequestHandler<TRequest, TResponse>.Handle(TRequest request, CancellationToken cancellationToken)
		=> new(Handle(request, cancellationToken));
}

/// <summary>
/// Convenience <see cref="Task{TResult}"/>-returning handler interface for requests with no response.
/// Returns <see cref="Unit"/> internally.
/// </summary>
/// <typeparam name="TRequest">The concrete request type to handle.</typeparam>
public interface ITaskRequestHandler<in TRequest> : ITaskRequestHandler<TRequest, Unit>
	where TRequest : IRequest<Unit>
{
}
