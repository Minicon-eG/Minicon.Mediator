namespace Minicon.Mediator;

/// <summary>
/// Marker interface for a request that streams a sequence of responses of type
/// <typeparamref name="TResponse"/> instead of a single one.
/// </summary>
/// <typeparam name="TResponse">The element type produced by the handler.</typeparam>
public interface IStreamRequest<out TResponse> : IBaseRequest
{
}
