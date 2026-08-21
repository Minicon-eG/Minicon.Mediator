# Minicon.Mediator

A lightweight mediator for .NET — request/response, streaming, notifications and pipeline
behaviors with exactly **one** runtime dependency
(`Microsoft.Extensions.DependencyInjection.Abstractions`).

The type names follow MediatR and `Mediator` (martinothamar), so code from either migrates with
little more than a `using` swap. See [Migrating](#migrating-from-mediator-martinothamar).

> Independent reimplementation by **minicon eG**. Not affiliated with or endorsed by
> the MediatR or Mediator projects. "MediatR" is a trademark of its respective owner; the familiar
> type names (`IRequest`, `INotification`, …) are kept for an easy migration, but this
> package lives in its own `Minicon.Mediator` namespace.

> **3.0.0 is a breaking change.** `IRequestHandler<,>.Handle` now returns `ValueTask<TResponse>`
> instead of `Task<TResponse>`. Handlers that return `Task` implement
> `ITaskRequestHandler<,>` instead — one line per handler, bodies unchanged. Everything else
> (`Send`, `Publish`, behaviors, streaming) is untouched.

## Install

```bash
dotnet add package Minicon.Mediator
```

Targets `net10.0`.

## Usage

```csharp
using Minicon.Mediator;
using Microsoft.Extensions.DependencyInjection;

// 1) Register — scans the given assembly for handlers
services.AddMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<Ping>());

// 2) Request / Response
public record Ping(string Message) : IRequest<string>;

public sealed class PingHandler : IRequestHandler<Ping, string>
{
    public ValueTask<string> Handle(Ping request, CancellationToken ct)
        => ValueTask.FromResult($"Pong: {request.Message}");
}

// …or, to keep a Task-returning handler:
public sealed class PingTaskHandler : ITaskRequestHandler<Ping, string>
{
    public Task<string> Handle(Ping request, CancellationToken ct)
        => Task.FromResult($"Pong: {request.Message}");
}

var answer = await mediator.Send(new Ping("hi"));   // "Pong: hi" — Send returns Task<T>

// 3) Notifications (fan-out to all handlers)
public record UserRegistered(string Email) : INotification;

public sealed class SendWelcome : INotificationHandler<UserRegistered>
{
    public Task Handle(UserRegistered n, CancellationToken ct) => /* ... */ Task.CompletedTask;
}

await mediator.Publish(new UserRegistered("a@b.de"));

// 4) Pipeline behaviors (logging, validation, transactions, …)
public sealed class LoggingBehavior<TReq, TRes> : IPipelineBehavior<TReq, TRes>
    where TReq : notnull
{
    public async Task<TRes> Handle(TReq req, RequestHandlerDelegate<TRes> next, CancellationToken ct)
    {
        // before
        var res = await next();
        // after
        return res;
    }
}
```

## Streaming

For handlers that produce many responses over time, use `IStreamRequest<T>` and
`CreateStream`. Elements are forwarded as the handler yields them — nothing is buffered,
so the consumer can `break` out early or cancel at any point.

```csharp
public record Tail(string File) : IStreamRequest<string>;

public sealed class TailHandler : IStreamRequestHandler<Tail, string>
{
    public async IAsyncEnumerable<string> Handle(Tail request, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var line in File.ReadLinesAsync(request.File, ct))
        {
            yield return line;
        }
    }
}

await foreach (var line in mediator.CreateStream(new Tail("app.log"), ct))
{
    Console.WriteLine(line);
}
```

Streams have their own behavior interface, `IStreamPipelineBehavior<TReq, TRes>`, which
wraps the handler the same way `IPipelineBehavior<,>` does for `Send`:

```csharp
public sealed class CountingBehavior<TReq, TRes> : IStreamPipelineBehavior<TReq, TRes>
    where TReq : notnull
{
    public async IAsyncEnumerable<TRes> Handle(
        TReq req, StreamHandlerDelegate<TRes> next, [EnumeratorCancellation] CancellationToken ct)
    {
        var count = 0;
        await foreach (var item in next(ct).WithCancellation(ct))
        {
            count++;
            yield return item;
        }
        // after — count is complete here
    }
}
```

Handlers are discovered by the same assembly scan as `IRequestHandler<,>`; stream
behaviors are registered manually (`services.AddTransient<IStreamPipelineBehavior<…>, …>()`).

### Two behavior shapes

`IPipelineBehavior<TRequest, TResponse>` accepts either of two `Handle` overloads — implement
whichever one your existing code uses:

```csharp
// MediatR shape — Task, next() / next(ct)
Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct);

// Mediator (martinothamar) shape — ValueTask, next(request, ct)
ValueTask<TResponse> Handle(TRequest request, CancellationToken ct, MessageHandlerDelegate<TRequest, TResponse> next);
```

The pipeline invokes the first one, so that is the allocation-free path; the second is reached through
a default interface implementation costing one closure and a `ValueTask`/`Task` conversion per call.
Implement exactly one — a behavior that implements neither compiles but recurses endlessly at runtime.

Note that the request handed to `next(request, ct)` is accepted for signature compatibility only: the
inner stages always run against the request the pipeline started with.

## Migrating from Mediator (martinothamar)

- `using Mediator;` → `using Minicon.Mediator;`
- `services.AddMediator()` (source-generated) → `services.AddMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<T>())`
- `IRequest<T>`, `IPipelineBehavior<,>`, `MessageHandlerDelegate<,>` keep their names; behaviors in the
  `(request, cancellationToken, next)` / `ValueTask` shape compile unchanged (see above).
- `cfg.ServiceLifetime` works as an alias of `cfg.Lifetime`. Note the default differs: `Transient` here
  (MediatR behavior) versus `Singleton` in `Mediator` — set it explicitly if you relied on the latter.
- `IRequestHandler<,>` returns `ValueTask<TResponse>` as it does in `Mediator`, so handlers compile
  unchanged.

Not carried over: `Send` returns `Task<T>`, not `ValueTask<T>` (`await` works either way, assigning to a
`ValueTask<T>` does not). `IMessage`, `ICommand`/`IQuery` and their handler interfaces have no
equivalent — use `IRequest`/`IRequest<T>`. Notifications always fan out sequentially; there is no
`INotificationPublisher` to swap in. `INotificationHandler` and `IStreamRequestHandler` keep their
MediatR shapes (`Task` / `IAsyncEnumerable`).

## Migrating from MediatR

Four replacements:

- `using MediatR;` → `using Minicon.Mediator;`
- `services.AddMediatR(...)` → `services.AddMediator(...)`
- `MediatRServiceConfiguration` → `MediatorServiceConfiguration`
- On request handlers: `IRequestHandler<,>` → `ITaskRequestHandler<,>`, keeping the
  `Task<TResponse> Handle(...)` body as is. (Or switch the return type to `ValueTask<TResponse>` and
  stay on `IRequestHandler<,>`.)

All other type names (`IRequest`, `IRequest<T>`, `IStreamRequest<T>`, `INotification`,
`IStreamRequestHandler<,>`, `INotificationHandler<>`, `IPipelineBehavior<,>`,
`IStreamPipelineBehavior<,>`, `ISender`, `IPublisher`, `IMediator`, `Unit`) are unchanged —
including notification handlers, which keep returning `Task`.

## License

MIT © minicon eG
