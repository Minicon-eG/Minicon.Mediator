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
		public ValueTask<string> Handle(Ping request, CancellationToken ct) => ValueTask.FromResult($"Pong: {request.Message}");
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

	// Deliberately the Task-returning (MediatR) shape, to cover the ITaskRequestHandler bridge.
	public sealed class DoWorkHandler : ITaskRequestHandler<DoWork, Unit>
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
		public ValueTask<string> Handle(Traced request, CancellationToken ct) { Trace.Add("handler"); return ValueTask.FromResult("ok"); }
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

	// ---- Pipeline behaviors, Mediator (martinothamar) shape ----

	// Mirrors behaviors migrated from `Mediator` 2.x: ValueTask, (request, ct, next) order,
	// and `next(request, ct)` instead of `next()`.
	public sealed class SafePipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
		where TRequest : IRequest<TResponse>
	{
		public async ValueTask<TResponse> Handle(
			TRequest request,
			CancellationToken cancellationToken,
			MessageHandlerDelegate<TRequest, TResponse> next)
		{
			Trace.Add("safe:before");
			try
			{
				return await next(request, cancellationToken);
			}
			finally
			{
				Trace.Add("safe:after");
			}
		}
	}

	[Fact]
	public async Task Message_shaped_behavior_wraps_handler()
	{
		Trace.Clear();
		var services = new ServiceCollection();
		services.AddMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatorTests>());
		services.AddTransient<IPipelineBehavior<Traced, string>, SafePipelineBehavior<Traced, string>>();
		var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

		var result = await mediator.Send(new Traced());

		Assert.Equal("ok", result);
		Assert.Equal(new[] { "safe:before", "handler", "safe:after" }, Trace);
	}

	[Fact]
	public async Task Message_and_request_shaped_behaviors_compose_in_registration_order()
	{
		Trace.Clear();
		var services = new ServiceCollection();
		services.AddMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatorTests>());
		services.AddTransient<IPipelineBehavior<Traced, string>, OuterBehavior>();
		services.AddTransient<IPipelineBehavior<Traced, string>, SafePipelineBehavior<Traced, string>>();
		var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

		var result = await mediator.Send(new Traced());

		Assert.Equal("ok", result);
		Assert.Equal(
			new[] { "outer:before", "safe:before", "handler", "safe:after", "outer:after" },
			Trace);
	}

	[Fact]
	public async Task Message_shaped_behavior_works_when_registered_as_open_generic()
	{
		Trace.Clear();
		var services = new ServiceCollection();
		services.AddMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatorTests>());
		services.AddTransient(typeof(IPipelineBehavior<,>), typeof(SafePipelineBehavior<,>));
		var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

		Assert.Equal("ok", await mediator.Send(new Traced()));
		Assert.Equal(new[] { "safe:before", "handler", "safe:after" }, Trace);

		// Also has to close over IRequest (i.e. IRequest<Unit>) without tripping the type constraint.
		Assert.Equal(Unit.Value, await mediator.Send(new DoWork()));
	}

	public record Failing : IRequest<string>;

	// Task shape again — an exception thrown by a bridged handler must surface identically.
	public sealed class FailingHandler : ITaskRequestHandler<Failing, string>
	{
		public Task<string> Handle(Failing request, CancellationToken ct) => throw new InvalidOperationException("boom");
	}

	public sealed class SwallowingBehavior : IPipelineBehavior<Failing, string>
	{
		public async ValueTask<string> Handle(Failing request, CancellationToken ct, MessageHandlerDelegate<Failing, string> next)
		{
			try
			{
				return await next(request, ct);
			}
			catch (InvalidOperationException ex)
			{
				return $"caught: {ex.Message}";
			}
		}
	}

	[Fact]
	public async Task Message_shaped_behavior_observes_handler_exceptions()
	{
		var services = new ServiceCollection();
		services.AddMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatorTests>());
		services.AddTransient<IPipelineBehavior<Failing, string>, SwallowingBehavior>();
		var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

		Assert.Equal("caught: boom", await mediator.Send(new Failing()));
	}

	public record Cancelled : IRequest<string>;

	public sealed class CancelledHandler : IRequestHandler<Cancelled, string>
	{
		public static CancellationToken Seen;
		public ValueTask<string> Handle(Cancelled request, CancellationToken ct) { Seen = ct; return ValueTask.FromResult("ok"); }
	}

	public sealed class TokenSwappingBehavior : IPipelineBehavior<Cancelled, string>
	{
		public ValueTask<string> Handle(Cancelled request, CancellationToken ct, MessageHandlerDelegate<Cancelled, string> next)
			=> next(request, new CancellationTokenSource().Token);
	}

	[Fact]
	public async Task Message_shaped_behavior_forwards_its_own_cancellation_token()
	{
		var services = new ServiceCollection();
		services.AddMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatorTests>());
		services.AddTransient<IPipelineBehavior<Cancelled, string>, TokenSwappingBehavior>();
		var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

		using var outer = new CancellationTokenSource();
		await mediator.Send(new Cancelled(), outer.Token);

		Assert.NotEqual(outer.Token, CancelledHandler.Seen);
		Assert.True(CancelledHandler.Seen.CanBeCanceled);
	}

	// ---- Handler shapes: ValueTask (default) vs. Task (ITaskRequestHandler bridge) ----

	public record CreateThing(string Name) : IRequest<string>;

	// Exactly the shape a handler migrated from Mediator (martinothamar) has.
	public class CreateThingHandler : IRequestHandler<CreateThing, string>
	{
		public async ValueTask<string> Handle(CreateThing request, CancellationToken cancellationToken)
		{
			await Task.Yield();
			return $"created: {request.Name}";
		}
	}

	public record RenameThing(string Name) : IRequest<string>;

	public sealed class RenameThingHandler : ITaskRequestHandler<RenameThing, string>
	{
		public async Task<string> Handle(RenameThing request, CancellationToken cancellationToken)
		{
			await Task.Yield();
			return $"renamed: {request.Name}";
		}
	}

	[Fact]
	public async Task Both_handler_shapes_are_discovered_and_dispatched()
	{
		var mediator = BuildMediator();

		Assert.Equal("created: a", await mediator.Send(new CreateThing("a")));
		Assert.Equal("renamed: b", await mediator.Send(new RenameThing("b")));
	}

	[Fact]
	public async Task Task_shaped_handler_is_registered_under_IRequestHandler()
	{
		// The bridge only works if DI resolves ITaskRequestHandler implementations through the
		// IRequestHandler service type the wrapper asks for.
		var services = new ServiceCollection();
		services.AddMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatorTests>());
		var provider = services.BuildServiceProvider();

		var handler = provider.GetRequiredService<IRequestHandler<RenameThing, string>>();
		Assert.IsType<RenameThingHandler>(handler);
		Assert.Equal("renamed: c", await handler.Handle(new RenameThing("c"), default));
	}

	[Fact]
	public async Task Both_handler_shapes_run_through_pipeline_behaviors()
	{
		var services = new ServiceCollection();
		services.AddMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatorTests>());
		services.AddTransient(typeof(IPipelineBehavior<,>), typeof(SafePipelineBehavior<,>));
		var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

		Assert.Equal("created: d", await mediator.Send(new CreateThing("d")));
		Assert.Equal("renamed: e", await mediator.Send(new RenameThing("e")));
	}

	// ---- Configuration ----

	[Fact]
	public void ServiceLifetime_is_an_alias_for_Lifetime()
	{
		var config = new MediatorServiceConfiguration();
		Assert.Equal(ServiceLifetime.Transient, config.Lifetime);
		Assert.Equal(ServiceLifetime.Transient, config.ServiceLifetime);

		config.ServiceLifetime = ServiceLifetime.Singleton;
		Assert.Equal(ServiceLifetime.Singleton, config.Lifetime);

		config.Lifetime = ServiceLifetime.Scoped;
		Assert.Equal(ServiceLifetime.Scoped, config.ServiceLifetime);
	}

	[Fact]
	public void ServiceLifetime_drives_handler_registration()
	{
		var services = new ServiceCollection();
		services.AddMediator(cfg =>
		{
			cfg.RegisterServicesFromAssemblyContaining<MediatorTests>();
			cfg.ServiceLifetime = ServiceLifetime.Singleton;
		});

		var descriptor = services.First(d => d.ServiceType == typeof(IRequestHandler<Ping, string>));
		Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
	}

	// ---- Guard clauses ----

	[Fact]
	public async Task Send_null_request_throws()
	{
		var mediator = BuildMediator();
		await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.Send<string>(null!));
	}
}
