using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace Minicon.Mediator.Internal;

/// <summary>
/// Concrete stream wrapper. Mirrors <see cref="RequestHandlerWrapperImpl{TRequest,TResponse}"/>:
/// handler and behaviors are resolved from the scoped <see cref="IServiceProvider"/>, then the
/// pipeline is walked with a stack-frame-per-stage recursion. No LINQ, no intermediate buffering —
/// elements are forwarded as the handler yields them.
/// </summary>
/// <typeparam name="TRequest">Concrete request type.</typeparam>
/// <typeparam name="TResponse">Element type of the stream.</typeparam>
internal sealed class StreamHandlerWrapperImpl<TRequest, TResponse> : StreamHandlerWrapper<TResponse>
	where TRequest : IStreamRequest<TResponse>
{
	public override IAsyncEnumerable<TResponse> Handle(IStreamRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
	{
		TRequest typedRequest = (TRequest)request;
		IStreamRequestHandler<TRequest, TResponse> handler = serviceProvider.GetRequiredService<IStreamRequestHandler<TRequest, TResponse>>();

		IEnumerable<IStreamPipelineBehavior<TRequest, TResponse>> behaviors = serviceProvider.GetServices<IStreamPipelineBehavior<TRequest, TResponse>>();
		IStreamPipelineBehavior<TRequest, TResponse>[] behaviorArray = behaviors as IStreamPipelineBehavior<TRequest, TResponse>[]
			?? ToArray(behaviors);

		if (behaviorArray.Length == 0)
		{
			// Hot path: skip pipeline construction entirely.
			return handler.Handle(typedRequest, cancellationToken);
		}

		return ExecutePipeline(behaviorArray, 0, typedRequest, handler, cancellationToken);
	}

	public override async IAsyncEnumerable<object?> Handle(object request, IServiceProvider serviceProvider, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		IAsyncEnumerable<TResponse> stream = Handle((IStreamRequest<TResponse>)request, serviceProvider, cancellationToken);
		await foreach (TResponse item in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			yield return item;
		}
	}

	private static IAsyncEnumerable<TResponse> ExecutePipeline(
		IStreamPipelineBehavior<TRequest, TResponse>[] behaviors,
		int index,
		TRequest request,
		IStreamRequestHandler<TRequest, TResponse> handler,
		CancellationToken cancellationToken)
	{
		if (index >= behaviors.Length)
		{
			return handler.Handle(request, cancellationToken);
		}

		// Capture the next stage as a delegate. The behavior may forward its own cancellation token.
		IStreamPipelineBehavior<TRequest, TResponse> behavior = behaviors[index];
		int nextIndex = index + 1;
		IAsyncEnumerable<TResponse> Next(CancellationToken ct) => ExecutePipeline(behaviors, nextIndex, request, handler, ct);
		return behavior.Handle(request, Next, cancellationToken);
	}

	private static IStreamPipelineBehavior<TRequest, TResponse>[] ToArray(IEnumerable<IStreamPipelineBehavior<TRequest, TResponse>> behaviors)
	{
		// ICollection fast-path keeps us off LINQ.
		if (behaviors is ICollection<IStreamPipelineBehavior<TRequest, TResponse>> collection)
		{
			IStreamPipelineBehavior<TRequest, TResponse>[] array = new IStreamPipelineBehavior<TRequest, TResponse>[collection.Count];
			collection.CopyTo(array, 0);
			return array;
		}

		List<IStreamPipelineBehavior<TRequest, TResponse>> list = new();
		foreach (IStreamPipelineBehavior<TRequest, TResponse> b in behaviors)
		{
			list.Add(b);
		}

		return list.ToArray();
	}
}
