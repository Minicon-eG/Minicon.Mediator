# Minicon.Mediator

A lightweight, **MediatR-API-compatible** mediator for .NET — request/response,
streaming, notifications and pipeline behaviors with exactly **one** runtime dependency
(`Microsoft.Extensions.DependencyInjection.Abstractions`).

> Independent reimplementation by **minicon eG**. Not affiliated with or endorsed by
> the MediatR project. "MediatR" is a trademark of its respective owner; the familiar
> type names (`IRequest`, `INotification`, …) are kept for an easy migration, but this
> package lives in its own `Minicon.Mediator` namespace.

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
    public Task<string> Handle(Ping request, CancellationToken ct)
        => Task.FromResult($"Pong: {request.Message}");
}

var answer = await mediator.Send(new Ping("hi"));   // "Pong: hi"

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
- Handlers must return `Task<T>` rather than `ValueTask<T>`, and `IMessage`/`ICommand`/`IQuery` have no
  equivalent — use `IRequest`/`IRequest<T>`.

## Migrating from MediatR

In most cases a single replacement is enough:

- `using MediatR;` → `using Minicon.Mediator;`
- `services.AddMediatR(...)` → `services.AddMediator(...)`
- `MediatRServiceConfiguration` → `MediatorServiceConfiguration`

All other type names (`IRequest`, `IRequest<T>`, `IStreamRequest<T>`, `INotification`,
`IRequestHandler<,>`, `IStreamRequestHandler<,>`, `INotificationHandler<>`,
`IPipelineBehavior<,>`, `IStreamPipelineBehavior<,>`, `ISender`, `IPublisher`, `IMediator`,
`Unit`) are unchanged.

## License

MIT © minicon eG
