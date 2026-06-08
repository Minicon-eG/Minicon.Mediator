namespace Minicon.Mediator;

/// <summary>
/// Combines <see cref="ISender"/> and <see cref="IPublisher"/>; the canonical mediator façade.
/// </summary>
public interface IMediator : ISender, IPublisher
{
}
