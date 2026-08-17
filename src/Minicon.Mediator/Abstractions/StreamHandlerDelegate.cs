namespace Minicon.Mediator;

/// <summary>
/// Delegate representing the next stage in a stream pipeline. Stream behaviors invoke this to
/// continue execution either with their own cancellation token or the ambient one.
/// </summary>
/// <typeparam name="TResponse">The element type of the stream.</typeparam>
public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<TResponse>(CancellationToken cancellationToken = default);
