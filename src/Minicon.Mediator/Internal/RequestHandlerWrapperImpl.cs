using Microsoft.Extensions.DependencyInjection;

namespace Minicon.Mediator.Internal;

/// <summary>
/// Concrete request wrapper. Resolves the handler and any pipeline behaviors from the
/// scoped <see cref="IServiceProvider"/>, then walks the pipeline using a stack-frame-per-stage
/// recursion. No LINQ, no <c>.Reverse().Aggregate()</c>, no <c>Task.ContinueWith</c>.
/// </summary>
/// <typeparam name="TRequest">Concrete request type.</typeparam>
/// <typeparam name="TResponse">Response type.</typeparam>
internal sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
	where TRequest : IRequest<TResponse>
{
	public override Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
	{
		TRequest typedRequest = (TRequest)request;
		IRequestHandler<TRequest, TResponse> handler = serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

		// Materialize behaviors once. DI typically returns an array already; the cast keeps allocations
		// minimal in the common case without forcing an extra .ToArray() in the rare non-array case.
		IEnumerable<IPipelineBehavior<TRequest, TResponse>> behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
		IPipelineBehavior<TRequest, TResponse>[] behaviorArray = behaviors as IPipelineBehavior<TRequest, TResponse>[]
			?? ToArray(behaviors);

		if (behaviorArray.Length == 0)
		{
			// Hot path: skip pipeline construction entirely.
			return handler.Handle(typedRequest, cancellationToken);
		}

		return ExecutePipeline(behaviorArray, 0, typedRequest, handler, cancellationToken);
	}

	public override async Task<object?> Handle(object request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
	{
		TResponse response = await Handle((IRequest<TResponse>)request, serviceProvider, cancellationToken).ConfigureAwait(false);
		return response;
	}

	private static Task<TResponse> ExecutePipeline(
		IPipelineBehavior<TRequest, TResponse>[] behaviors,
		int index,
		TRequest request,
		IRequestHandler<TRequest, TResponse> handler,
		CancellationToken cancellationToken)
	{
		if (index >= behaviors.Length)
		{
			return handler.Handle(request, cancellationToken);
		}

		// Capture the next stage as a delegate. The behavior may forward its own cancellation token.
		IPipelineBehavior<TRequest, TResponse> behavior = behaviors[index];
		int nextIndex = index + 1;
		Task<TResponse> Next(CancellationToken ct) => ExecutePipeline(behaviors, nextIndex, request, handler, ct);
		return behavior.Handle(request, Next, cancellationToken);
	}

	private static IPipelineBehavior<TRequest, TResponse>[] ToArray(IEnumerable<IPipelineBehavior<TRequest, TResponse>> behaviors)
	{
		// ICollection fast-path keeps us off LINQ.
		if (behaviors is ICollection<IPipelineBehavior<TRequest, TResponse>> collection)
		{
			IPipelineBehavior<TRequest, TResponse>[] array = new IPipelineBehavior<TRequest, TResponse>[collection.Count];
			collection.CopyTo(array, 0);
			return array;
		}

		List<IPipelineBehavior<TRequest, TResponse>> list = new();
		foreach (IPipelineBehavior<TRequest, TResponse> b in behaviors)
		{
			list.Add(b);
		}

		return list.ToArray();
	}
}
