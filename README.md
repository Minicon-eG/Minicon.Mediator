# Minicon.Mediator

A lightweight, **MediatR-API-compatible** mediator for .NET — request/response,
notifications and pipeline behaviors with exactly **one** runtime dependency
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

## Migrating from MediatR

In most cases a single replacement is enough:

- `using MediatR;` → `using Minicon.Mediator;`
- `services.AddMediatR(...)` → `services.AddMediator(...)`
- `MediatRServiceConfiguration` → `MediatorServiceConfiguration`

All other type names (`IRequest`, `IRequest<T>`, `INotification`, `IRequestHandler<,>`,
`INotificationHandler<>`, `IPipelineBehavior<,>`, `ISender`, `IPublisher`, `IMediator`,
`Unit`) are unchanged.

## License

MIT © minicon eG
