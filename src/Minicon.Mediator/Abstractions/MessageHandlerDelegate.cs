namespace Minicon.Mediator;

/// <summary>
/// Delegate representing the next stage in a request pipeline, carrying the request along.
/// This is the shape used by <c>Mediator</c> (martinothamar) style behaviors; it exists so that
/// behaviors written against that API compile unchanged. See <see cref="RequestHandlerDelegate{TResponse}"/>
/// for the MediatR-shaped equivalent.
/// </summary>
/// <remarks>
/// The <paramref name="request"/> argument is passed for signature compatibility. The inner stages of the
/// pipeline always run against the request the pipeline was started with — substituting a different
/// instance here does not replace it downstream.
/// </remarks>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="request">The request being handled.</param>
/// <param name="cancellationToken">Cancellation token forwarded to the next stage.</param>
public delegate ValueTask<TResponse> MessageHandlerDelegate<TRequest, TResponse>(
	TRequest request,
	CancellationToken cancellationToken = default)
	where TRequest : notnull;
