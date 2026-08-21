# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Was das ist

`Minicon.Mediator` ist eine eigenständige, **MediatR-API-kompatible** Mediator-Bibliothek für .NET
(Request/Response, Streaming, Notifications, Pipeline-Behaviors). Sie ist als NuGet-Paket gedacht und hat
genau **eine** Laufzeit-Abhängigkeit: `Microsoft.Extensions.DependencyInjection.Abstractions`.

Target Framework: `net10.0`.

## Befehle

```bash
dotnet build -c Release                 # baut die Solution (Minicon.Mediator.slnx)
dotnet test  -c Release                 # alle Tests (xUnit)
dotnet pack src/Minicon.Mediator/Minicon.Mediator.csproj -c Release -o ./artifacts   # NuGet-Paket + .snupkg

# Einzelner Test (Filter nach Testmethoden-Name):
dotnet test --filter "FullyQualifiedName~Publish_fans_out_to_all_handlers"
```

Release/Publish nach NuGet.org läuft über GitHub Actions (`.github/workflows/publish.yml`):
Push eines Tags `v*` auf `main` triggert `dotnet pack` + `dotnet nuget push` mit `secrets.NUGET_API_KEY`.
Die `Version` wird in `src/Minicon.Mediator/Minicon.Mediator.csproj` gepflegt (aktuell `2.2.0`) — vor einem
neuen Release dort hochzählen und passend taggen. `artifacts/` ist gitignored und enthält lokal ggf. alte
`.nupkg`-Stände; nicht als Versionsquelle verwenden.

## Architektur

Der Kern ist ein **typ-gekeyter Wrapper-Cache plus Reflection-erzeugte generische Wrapper**, der den
object-typisierten Eintritt (`Send`/`CreateStream`/`Publish`) auf stark typisierte Pfade umlenkt:

1. **`Internal/Mediator.cs`** — die `IMediator`-Implementierung. Hält **drei** statische
   `ConcurrentDictionary<Type, …>`-Caches (Request-, Stream- und Notification-Wrapper). Pro konkretem
   Request-/Notification-Typ wird **einmalig** per Reflection (`MakeGenericType` + `Activator.CreateInstance`)
   ein generischer Wrapper gebaut und gecacht. Cache-Miss-Factories sind **statische Methoden**
   (`CreateRequestWrapper`/`CreateStreamWrapper`/`CreateNotificationWrapper`), um Closure-Allokationen
   zu vermeiden. Die Caches sind `static` — also prozessweit, nicht pro DI-Container.

2. **`Internal/RequestHandlerWrapper[Impl].cs`** — abstrakte Basis + generische Impl. `…Impl<TRequest,TResponse>`
   löst Handler und `IPipelineBehavior<,>` aus dem **scoped** `IServiceProvider` auf und führt die Pipeline
   per **rekursivem Index-Dispatch** aus (`ExecutePipeline`), nicht per `.Reverse().Aggregate()`. Hot Path:
   ohne Behaviors wird direkt der Handler aufgerufen, ohne Pipeline-Aufbau.

3. **`Internal/StreamHandlerWrapper[Impl].cs`** — dasselbe Muster für `IAsyncEnumerable<T>`:
   `IStreamRequestHandler<,>` + `IStreamPipelineBehavior<,>`, rekursiver Index-Dispatch, gleicher
   0-Behaviors-Fast-Path. **Nichts wird gepuffert** — Elemente werden weitergereicht, sobald der Handler
   sie yieldet (Consumer kann jederzeit `break`en/canceln). Das ist ein Vertragsversprechen aus der README,
   also beim Ändern keine Zwischen-Collection einziehen.

4. **`Internal/NotificationHandlerWrapper[Impl].cs`** — sequentielle Fan-out-Dispatch an alle Handler.
   Fast Path bei genau einem Handler (keine Exception-Aggregation); bei mehreren wird lazy zu
   `AggregateException` aggregiert, einzelne Exceptions per `ExceptionDispatchInfo` rethrown.
   `OperationCanceledException` wird nicht aggregiert.

5. **`Extensions/MediatorServiceExtensions.cs`** — `AddMediator(...)`. **Wichtig:** liegt bewusst im
   Namespace `Microsoft.Extensions.DependencyInjection`, damit MediatR-Aufrufstellen ohne neue `using`s
   kompilieren. Registriert `IMediator`/`ISender`/`IPublisher` als **Scoped** (über eigene, LINQ-freie
   `TryAddScoped`-Helper statt `Microsoft.Extensions.DependencyInjection.Extensions`) und scannt die
   konfigurierten Assemblies nach `IRequestHandler<,>`, `IStreamRequestHandler<,>` und
   `INotificationHandler<>` (Lifetime: Default **Transient**). `ReflectionTypeLoadException` wird toleriert
   (nur ladbare Typen werden registriert).
   **Behaviors werden bewusst nicht gescannt** — `IPipelineBehavior<,>` und `IStreamPipelineBehavior<,>`
   registriert der Aufrufer selbst; die Reihenfolge der Registrierung ist die Ausführungsreihenfolge.

6. **`MediatorServiceConfiguration.cs`** (Projektwurzel, nicht `Extensions/`) — das Konfigurationsobjekt für
   `AddMediator(cfg => …)`: `RegisterServicesFromAssembly(ies)`, `RegisterServicesFromAssemblyContaining<T>()`,
   `Lifetime`. Spiegelt `MediatRServiceConfiguration` aus MediatR v12+.

7. **`Abstractions/`** — das öffentliche, MediatR-kompatible Vertrags-Surface (`IRequest`, `IRequest<T>`,
   `IStreamRequest<T>`, `INotification`, `IRequestHandler<,>`, `IStreamRequestHandler<,>`,
   `INotificationHandler<>`, `IPipelineBehavior<,>`, `IStreamPipelineBehavior<,>`, `ISender`, `IPublisher`,
   `IMediator`, `Unit`, `RequestHandlerDelegate<T>`, `StreamHandlerDelegate<T>`, `MessageHandlerDelegate<,>`,
   `IBaseRequest`). Diese Namen sind absichtlich identisch zu MediatR — nur Namespace (`Minicon.Mediator`)
   und `AddMediatR`→`AddMediator` unterscheiden sich. `MessageHandlerDelegate<,>` stammt zusätzlich aus
   `Mediator` (martinothamar) — siehe Konventionen unten.

## Konventionen / Constraints

- **API-Kompatibilität zu MediatR ist ein hartes Ziel.** Typnamen/Signaturen in `Abstractions/` nicht
  umbenennen oder umformen — Migrationsversprechen aus der README (1:1 `using`-Tausch) muss gelten.
  Wird das Vertrags-Surface erweitert, gehört der neue Typ auch in die README-Migrationsliste.
- **Genau eine Laufzeit-Abhängigkeit.** Keine weiteren `PackageReference`s ins Hauptprojekt aufnehmen.
  Das ist der Grund für manche „Rad neu erfunden"-Stellen (eigene `TryAddScoped`-Helper, eigenes
  `ToArray` statt LINQ) — nicht „aufräumen".
- `RequestHandlerDelegate<TResponse>` nimmt einen **optionalen** `CancellationToken` — Behaviors können
  `next()` oder `next(ct)` aufrufen; die Pipeline reicht den Token weiter. `StreamHandlerDelegate<T>`
  verhält sich analog.
- **`IPipelineBehavior<,>` hat zwei `Handle`-Overloads mit gegenseitigen Default-Implementierungen**
  (Default Interface Methods), damit sowohl MediatR- als auch `Mediator`-Behaviors (martinothamar, 2.x)
  unverändert kompilieren:
  `Handle(request, RequestHandlerDelegate<TRes> next, ct)` → `Task` (MediatR) und
  `Handle(request, ct, MessageHandlerDelegate<TReq,TRes> next)` → `ValueTask` (Mediator).
  Der Wrapper ruft **immer die MediatR-Variante** auf; das hält deren Hot Path allokationsfrei, während
  die Mediator-Variante über den Default adaptiert wird. Beim Ändern beachten:
  - Wer **keinen** der beiden Overloads implementiert, erzeugt zur Laufzeit Endlosrekursion — bewusst
    in Kauf genommen, im XML-Doc dokumentiert.
  - Der an `next(request, ct)` übergebene Request wird **ignoriert**; die inneren Stufen laufen gegen den
    ursprünglichen Request. Request-Ersetzung ließe sich nur mit typ-gecachter Reflection im Wrapper
    umsetzen — bewusst nicht gemacht.
  - `IPipelineBehavior` hat deshalb **kein `in TRequest`** mehr (MediatR hat es): `MessageHandlerDelegate<TRequest,…>`
    an Parameterposition ist mit kontravariantem `TRequest` nicht varianz-gültig.
- Performance ist explizites Designziel (siehe XML-Doc-Kommentare in `Internal/Mediator.cs`): kein LINQ
  auf Hot Paths, statische Factories, Fast Paths bei 0/1 Elementen. Diese Eigenschaften beim Ändern
  erhalten.
- **Code-Stil im `src/`-Projekt:** Tabs zur Einrückung, **explizite Typen statt `var`**, `for`-Schleifen
  mit Index statt `foreach`/LINQ in den Internals, `.ConfigureAwait(false)` auf allen Awaits. Die Tests
  dürfen lockerer sein (dort ist `var` üblich).
- `GlobalUsings.cs` enthält die projektweiten Usings — Abstraktionen werden dort oft schon importiert.

## Tests

xUnit v2, zwei Dateien: `MediatorTests.cs` (Send/Publish/Behaviors/DI) und `StreamTests.cs`.
Requests und Handler werden als **nested types innerhalb der Testklasse** deklariert und über
`RegisterServicesFromAssemblyContaining<MediatorTests>()` mitgescannt — neue Testszenarien nach diesem
Muster ergänzen, statt separate Fixture-Dateien anzulegen.
