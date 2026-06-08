namespace Minicon.Mediator;

/// <summary>
/// Publishes notifications to all registered handlers.
/// </summary>
public interface IPublisher
{
	/// <summary>
	/// Publishes a notification to all registered <see cref="INotificationHandler{TNotification}"/> instances.
	/// </summary>
	Task Publish(object notification, CancellationToken cancellationToken = default);

	/// <summary>
	/// Publishes a notification to all registered <see cref="INotificationHandler{TNotification}"/> instances.
	/// </summary>
	Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
		where TNotification : INotification;
}
