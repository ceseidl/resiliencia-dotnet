using System.Net.Http.Json;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace FreteResiliente;

public class FreteClient(HttpClient http)
{
    private static readonly Cotacao TabelaPadrao =
        new(29.90m, "tabela-padrao");

    public async Task<Cotacao> CotarAsync()
    {
        try
        {
            var cotacao = await http
                .GetFromJsonAsync<Cotacao>("/cotacao");
            return cotacao ?? TabelaPadrao;
        }
        catch (Exception e) when (e is HttpRequestException
            or BrokenCircuitException
            or TimeoutRejectedException)
        {
            // RESILIÊNCIA: fallback. Funciona degradado
            // (frete padrão) em vez de derrubar o pedido.
            return TabelaPadrao;
        }
    }
}
