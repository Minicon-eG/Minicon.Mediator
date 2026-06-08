namespace Minicon.Mediator;

/// <summary>
/// Delegate representing the next stage in a request pipeline. Pipeline behaviors invoke this to
/// continue execution either with their own cancellation token or the ambient one.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken = default);
