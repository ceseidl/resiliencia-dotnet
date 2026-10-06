# Fault Tolerance vs Resilience in .NET

English | [Português](README.pt-BR.md)

A runnable .NET 10 example that shows the difference between two concepts that are often confused:

- **Fault tolerance**: the system keeps working when a component fails, ideally without the user noticing. Mechanism here: **redundancy** (a load balancer that fails over to another instance).
- **Resilience**: the system copes with failures and degradation and recovers from them. Mechanism here: **timeout, retry with exponential backoff and jitter, circuit breaker and fallback**, using `Microsoft.Extensions.Http.Resilience` (built on Polly v8).

## How the layers fit together

```
FreteClient            (fallback: default freight table)
  └─ Resilience        (total timeout → retry → circuit breaker
       │                 → per-attempt timeout)
       └─ Fault tolerance   (balancer: instance A → instance B)
```

Each retry attempt goes through the balancer, which already tries every instance. If everything fails, the client returns a degraded answer instead of breaking the caller.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Run

```bash
dotnet build
dotnet run --project src/FreteResiliente --no-build
```

No external services are needed: the freight service instances are simulated in-process (`InstanciaSimulada`), each with a script that says how it behaves on every call (ok, error or slow).

## What the output shows

| Scenario | What happens | Concept |
|---|---|---|
| Instance A down | The balancer tries B in the same attempt; the caller gets the real answer | Fault tolerance |
| Transient failure | Single instance fails twice, the retry succeeds on the 3rd call | Resilience (retry) |
| Everything down | After 4 attempts the fallback returns the default table | Resilience (fallback) |
| Circuit open | The next call does not even reach the dependency; fallback answers in ~1 ms | Resilience (circuit breaker) |
| First call hangs | The 300 ms per-attempt timeout cuts it and the retry succeeds | Resilience (timeout) |

Timings vary on each run.

## Project structure

```
src/FreteResiliente/
├── Cotacao.cs             # response contract
├── InstanciaSimulada.cs   # simulated service instance (scripted behavior)
├── BalanceadorHandler.cs  # fault tolerance: failover between instances
├── FreteClient.cs         # consumer with fallback
└── Program.cs             # resilience pipeline and the 5 scenarios
```

## Production notes

- Retry only idempotent operations (or use an idempotency key).
- Always set timeouts, and use jitter to avoid retry storms.
- One circuit breaker per dependency.
- Flag degraded answers (here `Origem = "tabela-padrao"`) so logs and metrics can see them.
- Redundant instances sharing the same database or zone fail together: redundancy needs independent failures.
- A real balancer also uses health checks to take sick instances out of rotation.

## Links

- [Resilience for HTTP in .NET (Microsoft Learn)](https://learn.microsoft.com/dotnet/core/resilience/http-resilience)
- [Polly documentation](https://www.pollydocs.org)
