namespace Minicon.Mediator;

/// <summary>
/// Marker interface for a request that does not return a value.
/// Internally treated as <see cref="IRequest{Unit}"/>.
/// </summary>
public interface IRequest : IRequest<Unit>, IBaseRequest
{
}

/// <summary>
/// Marker interface for a request that returns a response of type <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TResponse">The response type produced by the handler.</typeparam>
public interface IRequest<out TResponse> : IBaseRequest
{
}
