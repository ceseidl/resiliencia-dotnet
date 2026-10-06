using System.Net;
using System.Net.Http.Json;

namespace FreteResiliente;

public enum Comportamento { Ok, Erro, Lenta }

// EN: Simulates an instance of the freight service. The script says how
// EN: it behaves on each call (1st, 2nd, 3rd...).
// PT: Simula uma instância do serviço de frete. O roteiro diz como
// PT: ela se comporta a cada chamada (1ª, 2ª, 3ª...).
public class InstanciaSimulada(
    string nome,
    Func<int, Comportamento> roteiro) : HttpMessageHandler
{
    public string Nome { get; } = nome;
    public int Chamadas { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        Chamadas++;
        switch (roteiro(Chamadas))
        {
            case Comportamento.Erro:
                return new HttpResponseMessage(
                    HttpStatusCode.ServiceUnavailable);

            case Comportamento.Lenta:
                await Task.Delay(Timeout.Infinite, ct);
                break;
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new Cotacao(24.90m, Nome))
        };
    }
}
