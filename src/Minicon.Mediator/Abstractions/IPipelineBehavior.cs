namespace Minicon.Mediator;

/// <summary>
/// Pipeline behavior intercepting request handling, similar to a middleware.
/// Behaviors are invoked in registration order and must call <c>next</c>
/// exactly once to continue the pipeline (or short-circuit deliberately).
/// </summary>
/// <remarks>
/// <para>
/// Two overloads of <c>Handle</c> are offered so that behaviors written against either MediatR or
/// <c>Mediator</c> (martinothamar) compile unchanged. Implement <b>exactly one</b> of them — each has a
/// default implementation that forwards to the other:
/// </para>
/// <list type="bullet">
///   <item><see cref="Handle(TRequest, RequestHandlerDelegate{TResponse}, CancellationToken)"/> — MediatR
///         shape, <c>Task</c>-based, <c>next()</c> / <c>next(ct)</c>. This is the one the pipeline calls,
///         so it is the allocation-free path.</item>
///   <item><see cref="Handle(TRequest, CancellationToken, MessageHandlerDelegate{TRequest, TResponse})"/> —
///         <c>Mediator</c> shape, <c>ValueTask</c>-based, <c>next(request, ct)</c>. Reached through the
///         default implementation above, which costs one closure plus a
///         <see cref="ValueTask{TResult}"/>/<see cref="Task{TResult}"/> conversion per invocation.</item>
/// </list>
/// <para>
/// Implementing <b>neither</b> compiles but recurses endlessly at runtime — a behavior that overrides
/// nothing has no work to do and should not be registered.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IPipelineBehavior<TRequest, TResponse>
	where TRequest : notnull
{
	/// <summary>
	/// Wraps the inner handler invocation with custom logic (MediatR shape).
	/// </summary>
	/// <param name="request">The request being handled.</param>
	/// <param name="next">Delegate that invokes the next behavior or final handler.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
		=> Handle(request, cancellationToken, (_, ct) => new ValueTask<TResponse>(next(ct))).AsTask();

	/// <summary>
	/// Wraps the inner handler invocation with custom logic (<c>Mediator</c> shape).
	/// </summary>
	/// <param name="request">The request being handled.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <param name="next">Delegate that invokes the next behavior or final handler.</param>
	ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken, MessageHandlerDelegate<TRequest, TResponse> next)
		=> new(Handle(request, ct => next(request, ct).AsTask(), cancellationToken));
}
