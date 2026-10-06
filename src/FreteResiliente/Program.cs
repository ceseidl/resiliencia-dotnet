using System.Diagnostics;
using FreteResiliente;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;

static FreteClient Criar(params InstanciaSimulada[] instancias)
{
    var services = new ServiceCollection();

    services.AddHttpClient<FreteClient>(c =>
            c.BaseAddress = new Uri("http://frete"))
        .ConfigurePrimaryHttpMessageHandler(
            () => new BalanceadorHandler(instancias))
        .AddResilienceHandler("frete", pipeline =>
        {
            pipeline.AddTimeout(TimeSpan.FromSeconds(2));

            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(100),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = args => ValueTask.FromResult(
                    HttpClientResiliencePredicates
                        .IsTransient(args.Outcome)
                    && args.Outcome.Exception
                        is not BrokenCircuitException)
            });

            pipeline.AddCircuitBreaker(
                new HttpCircuitBreakerStrategyOptions
                {
                    SamplingDuration = TimeSpan.FromSeconds(10),
                    MinimumThroughput = 4,
                    FailureRatio = 0.5,
                    BreakDuration = TimeSpan.FromSeconds(30)
                });

            pipeline.AddTimeout(TimeSpan.FromMilliseconds(300));
        });

    return services.BuildServiceProvider()
        .GetRequiredService<FreteClient>();
}

static async Task Executar(
    string titulo, FreteClient cliente,
    params InstanciaSimulada[] instancias)
{
    Console.WriteLine($"\n== {titulo}");
    var relogio = Stopwatch.StartNew();
    var cotacao = await cliente.CotarAsync();

    Console.WriteLine(
        "Response / Resposta: "
        + $"R$ {cotacao.Valor} via {cotacao.Origem} "
        + $"in / em {relogio.ElapsedMilliseconds} ms");
    foreach (var i in instancias)
        Console.WriteLine(
            $"  {i.Nome}: {i.Chamadas} call(s) / chamada(s)");
}

// EN: 1. Fault tolerance: A is down, B answers. Nobody notices.
// PT: 1. Tolerância a falhas: A caiu, B atende. Ninguém percebe.
var a = new InstanciaSimulada("A", _ => Comportamento.Erro);
var b = new InstanciaSimulada("B", _ => Comportamento.Ok);
await Executar(
    "Fault tolerance: instance A down / "
    + "Tolerância: instância A fora",
    Criar(a, b), a, b);

// EN: 2. Resilience: fails twice, then recovers. The retry
// EN: handles it.
// PT: 2. Resiliência: falha 2 vezes e volta. O retry recupera.
var c = new InstanciaSimulada(
    "C", n => n <= 2 ? Comportamento.Erro : Comportamento.Ok);
await Executar(
    "Resilience: transient failure / "
    + "Resiliência: falha transitória",
    Criar(c), c);

// EN: 3. Resilience: everything down. Fallback and circuit
// EN: breaker.
// PT: 3. Resiliência: tudo fora. Fallback e circuit breaker.
var d = new InstanciaSimulada("D", _ => Comportamento.Erro);
var cliente = Criar(d);
await Executar(
    "Degradation: all down / Degradação: tudo fora", cliente, d);
await Executar(
    "Circuit open: does not even try / "
    + "Circuito aberto: nem tenta",
    cliente, d);

// EN: 4. Resilience: hung instance. The timeout cuts it and
// EN: retries.
// PT: 4. Resiliência: instância travada. O timeout corta e refaz.
var e = new InstanciaSimulada(
    "E", n => n == 1 ? Comportamento.Lenta : Comportamento.Ok);
await Executar(
    "Timeout: 1st call hangs / "
    + "Timeout: 1ª chamada trava",
    Criar(e), e);
