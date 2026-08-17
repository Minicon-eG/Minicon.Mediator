using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Minicon.Mediator.Tests;

public class StreamTests
{
	private static IMediator BuildMediator(Action<IServiceCollection>? extra = null)
	{
		var services = new ServiceCollection();
		services.AddMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<StreamTests>());
		extra?.Invoke(services);
		return services.BuildServiceProvider().GetRequiredService<IMediator>();
	}

	// ---- Streaming request / response ----

	public record Count(int To) : IStreamRequest<int>;

	public sealed class CountHandler : IStreamRequestHandler<Count, int>
	{
		public static int Started;

		public async IAsyncEnumerable<int> Handle(Count request, [EnumeratorCancellation] CancellationToken ct)
		{
			Started++;
			for (int i = 1; i <= request.To; i++)
			{
				ct.ThrowIfCancellationRequested();
				await Task.Yield();
				yield return i;
			}
		}
	}

	[Fact]
	public async Task CreateStream_routes_to_correct_handler_and_yields_all_elements()
	{
		var mediator = BuildMediator();

		var received = new List<int>();
		await foreach (int value in mediator.CreateStream(new Count(3)))
		{
			received.Add(value);
		}

		Assert.Equal(new[] { 1, 2, 3 }, received);
	}

	[Fact]
	public async Task CreateStream_streams_lazily_and_stops_when_consumer_breaks()
	{
		var mediator = BuildMediator();

		var received = new List<int>();
		await foreach (int value in mediator.CreateStream(new Count(1_000_000)))
		{
			received.Add(value);
			if (received.Count == 2)
			{
				break;
			}
		}

		// A buffering implementation would never get here — the handler counts to a million.
		Assert.Equal(new[] { 1, 2 }, received);
	}

	[Fact]
	public async Task CreateStream_honours_cancellation()
	{
		var mediator = BuildMediator();
		using var cts = new CancellationTokenSource();

		var received = new List<int>();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
		{
			await foreach (int value in mediator.CreateStream(new Count(100), cts.Token))
			{
				received.Add(value);
				if (received.Count == 2)
				{
					cts.Cancel();
				}
			}
		});

		Assert.Equal(new[] { 1, 2 }, received);
	}

	// ---- Untyped overload ----

	[Fact]
	public async Task CreateStream_untyped_yields_boxed_elements()
	{
		var mediator = BuildMediator();

		var received = new List<object?>();
		await foreach (object? value in mediator.CreateStream((object)new Count(2)))
		{
			received.Add(value);
		}

		Assert.Equal(new object?[] { 1, 2 }, received);
	}

	[Fact]
	public void CreateStream_untyped_rejects_non_request()
	{
		var mediator = BuildMediator();
		Assert.Throws<ArgumentException>(() => mediator.CreateStream(new object()));
	}

	[Fact]
	public void CreateStream_null_request_throws()
	{
		var mediator = BuildMediator();
		Assert.Throws<ArgumentNullException>(() => mediator.CreateStream<int>(null!));
	}

	// ---- Stream pipeline behaviors ----

	public static readonly List<string> Trace = new();

	public record Traced : IStreamRequest<string>;

	public sealed class TracedHandler : IStreamRequestHandler<Traced, string>
	{
		public async IAsyncEnumerable<string> Handle(Traced request, [EnumeratorCancellation] CancellationToken ct)
		{
			Trace.Add("handler:start");
			yield return "a";
			yield return "b";
			Trace.Add("handler:end");
			await Task.CompletedTask;
		}
	}

	public sealed class OuterBehavior : IStreamPipelineBehavior<Traced, string>
	{
		public async IAsyncEnumerable<string> Handle(Traced request, StreamHandlerDelegate<string> next, [EnumeratorCancellation] CancellationToken ct)
		{
			Trace.Add("outer:before");
			await foreach (string item in next(ct).WithCancellation(ct))
			{
				yield return item.ToUpperInvariant();
			}

			Trace.Add("outer:after");
		}
	}

	public sealed class InnerBehavior : IStreamPipelineBehavior<Traced, string>
	{
		public async IAsyncEnumerable<string> Handle(Traced request, StreamHandlerDelegate<string> next, [EnumeratorCancellation] CancellationToken ct)
		{
			Trace.Add("inner:before");
			await foreach (string item in next(ct).WithCancellation(ct))
			{
				yield return item;
			}

			Trace.Add("inner:after");
		}
	}

	[Fact]
	public async Task Stream_pipeline_behaviors_wrap_handler_in_registration_order()
	{
		Trace.Clear();
		var mediator = BuildMediator(services =>
		{
			services.AddTransient<IStreamPipelineBehavior<Traced, string>, OuterBehavior>();
			services.AddTransient<IStreamPipelineBehavior<Traced, string>, InnerBehavior>();
		});

		var received = new List<string>();
		await foreach (string value in mediator.CreateStream(new Traced()))
		{
			received.Add(value);
		}

		Assert.Equal(new[] { "A", "B" }, received);
		Assert.Equal(
			new[] { "outer:before", "inner:before", "handler:start", "handler:end", "inner:after", "outer:after" },
			Trace);
	}

	// ---- Missing handler ----

	public record Unhandled : IStreamRequest<int>;

	[Fact]
	public void CreateStream_without_handler_throws()
	{
		var mediator = BuildMediator();
		Assert.Throws<InvalidOperationException>(() => mediator.CreateStream(new Unhandled()));
	}
}
