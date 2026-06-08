using Microsoft.Extensions.DependencyInjection;

namespace Minicon.Mediator.Internal;

/// <summary>
/// Concrete notification wrapper. Dispatches sequentially to all registered handlers and
/// aggregates exceptions only when more than one is thrown (lazy allocation).
/// </summary>
/// <typeparam name="TNotification">The notification type.</typeparam>
internal sealed class NotificationHandlerWrapperImpl<TNotification> : NotificationHandlerWrapper
	where TNotification : INotification
{
	public override async Task Handle(object notification, IServiceProvider serviceProvider, CancellationToken cancellationToken)
	{
		TNotification typedNotification = (TNotification)notification;

		IEnumerable<INotificationHandler<TNotification>> handlers = serviceProvider.GetServices<INotificationHandler<TNotification>>();
		INotificationHandler<TNotification>[] array = handlers as INotificationHandler<TNotification>[]
			?? ToArray(handlers);

		if (array.Length == 0)
		{
			return;
		}

		// Fast path: single handler — no aggregation, no extra try/catch overhead.
		if (array.Length == 1)
		{
			await array[0].Handle(typedNotification, cancellationToken).ConfigureAwait(false);
			return;
		}

		Exception? firstException = null;
		List<Exception>? exceptions = null;

		for (int i = 0; i < array.Length; i++)
		{
			try
			{
				await array[i].Handle(typedNotification, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				if (firstException is null)
				{
					firstException = ex;
				}
				else
				{
					exceptions ??= new List<Exception> { firstException };
					exceptions.Add(ex);
				}
			}
		}

		if (exceptions is not null)
		{
			throw new AggregateException($"One or more handlers for notification '{typeof(TNotification).Name}' threw.", exceptions);
		}

		if (firstException is not null)
		{
			// Preserve original stack via re-throw helper.
			System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstException).Throw();
		}
	}

	private static INotificationHandler<TNotification>[] ToArray(IEnumerable<INotificationHandler<TNotification>> handlers)
	{
		if (handlers is ICollection<INotificationHandler<TNotification>> collection)
		{
			INotificationHandler<TNotification>[] array = new INotificationHandler<TNotification>[collection.Count];
			collection.CopyTo(array, 0);
			return array;
		}

		List<INotificationHandler<TNotification>> list = new();
		foreach (INotificationHandler<TNotification> h in handlers)
		{
			list.Add(h);
		}

		return list.ToArray();
	}
}
