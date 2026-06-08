namespace Minicon.Mediator.Internal;

/// <summary>
/// Untyped notification handler wrapper used by <see cref="Mediator.Publish(object, CancellationToken)"/>.
/// </summary>
internal abstract class NotificationHandlerWrapper
{
	public abstract Task Handle(object notification, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
