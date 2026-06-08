namespace Minicon.Mediator;

/// <summary>
/// Defines a handler that consumes a notification of type <typeparamref name="TNotification"/>.
/// </summary>
/// <typeparam name="TNotification">The notification type.</typeparam>
public interface INotificationHandler<in TNotification>
	where TNotification : INotification
{
	/// <summary>
	/// Handles the published notification.
	/// </summary>
	/// <param name="notification">The notification.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task Handle(TNotification notification, CancellationToken cancellationToken);
}
