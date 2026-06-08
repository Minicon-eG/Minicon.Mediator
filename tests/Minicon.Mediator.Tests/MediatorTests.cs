using Microsoft.Extensions.DependencyInjection;
using Minicon.Mediator;
using Xunit;

namespace Minicon.Mediator.Tests;

public class MediatorTests
{
	private static IMediator BuildMediator(Action<MediatorServiceConfiguration>? extra = null)
	{
		var services = new ServiceCollection();
		services.AddMediator(cfg =>
		{
			cfg.RegisterServicesFromAssemblyContaining<MediatorTests>();
			extra?.Invoke(cfg);
		});
		return services.BuildServiceProvider().GetRequiredService<IMediator>();
	}

	// ---- Request / Response ----

	public record Ping(string Message) : IRequest<string>;

	public sealed class PingHandler : IRequestHandler<Ping, string>
	{
		public Task<string> Handle(Ping request, CancellationToken ct) => Task.FromResult($"Pong: {request.Message}");
	}

	[Fact]
	public async Task Send_routes_to_correct_handler_and_returns_response()
	{
		var mediator = BuildMediator();
		var result = await mediator.Send(new Ping("hi"));
		Assert.Equal("Pong: hi", result);
	}

	// ---- Request without response (Unit) ----

	public record DoWork : IRequest;

	public sealed class DoWorkHandler : IRequestHandler<DoWork, Unit>
	{
		public static bool Handled;
		public Task<Unit> Handle(DoWork request, CancellationToken ct)
		{
			Handled = true;
			return Unit.Task;
		}
	}

	[Fact]
	public async Task Send_void_request_invokes_handler_and_returns_unit()
	{
		DoWorkHandler.Handled = false;
		var mediator = BuildMediator();
		var unit = await mediator.Send(new DoWork());
		Assert.True(DoWorkHandler.Handled);
		Assert.Equal(Unit.Value, unit);
	}

	// ---- Notifications (fan-out) ----

	public record Signal : INotification;

	public sealed class HandlerA : INotificationHandler<Signal>
	{
		public static int Count;
		public Task Handle(Signal n, CancellationToken ct) { Count++; return Task.CompletedTask; }
	}

	public sealed class HandlerB : INotificationHandler<Signal>
	{
		public static int Count;
		public Task Handle(Signal n, CancellationToken ct) { Count++; return Task.CompletedTask; }
	}

	[Fact]
	public async Task Publish_fans_out_to_all_handlers()
	{
		HandlerA.Count = 0;
		HandlerB.Count = 0;
		var mediator = BuildMediator();
		await mediator.Publish(new Signal());
		Assert.Equal(1, HandlerA.Count);
		Assert.Equal(1, HandlerB.Count);
	}

	// ---- Pipeline behaviors (order + wrapping) ----

	public static readonly List<string> Trace = new();

	public record Traced : IRequest<string>;

	public sealed class TracedHandler : IRequestHandler<Traced, string>
	{
		public Task<string> Handle(Traced request, CancellationToken ct) { Trace.Add("handler"); return Task.FromResult("ok"); }
	}

	public sealed class OuterBehavior : IPipelineBehavior<Traced, string>
	{
		public async Task<string> Handle(Traced request, RequestHandlerDelegate<string> next, CancellationToken ct)
		{
			Trace.Add("outer:before");
			var res = await next();
			Trace.Add("outer:after");
			return res;
		}
	}

	[Fact]
	public async Task Pipeline_behavior_wraps_handler()
	{
		Trace.Clear();
		var mediator = BuildMediator(cfg => { });
		var services = new ServiceCollection();
		services.AddMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatorTests>());
		services.AddTransient<IPipelineBehavior<Traced, string>, OuterBehavior>();
		var withPipeline = services.BuildServiceProvider().GetRequiredService<IMediator>();

		var result = await withPipeline.Send(new Traced());

		Assert.Equal("ok", result);
		Assert.Equal(new[] { "outer:before", "handler", "outer:after" }, Trace);
	}

	// ---- Guard clauses ----

	[Fact]
	public async Task Send_null_request_throws()
	{
		var mediator = BuildMediator();
		await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.Send<string>(null!));
	}
}
