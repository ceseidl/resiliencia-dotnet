namespace FreteResiliente;

// TOLERÂNCIA A FALHAS: redundância. Se uma instância falha,
// a requisição vai para a próxima e o chamador nem percebe.
public class BalanceadorHandler(
    IReadOnlyList<HttpMessageHandler> instancias)
    : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage? ultima = null;

        foreach (var instancia in instancias)
        {
            using var invoker = new HttpMessageInvoker(
                instancia, disposeHandler: false);

            var resposta = await invoker.SendAsync(request, ct);
            if (resposta.IsSuccessStatusCode)
                return resposta;

            ultima?.Dispose();
            ultima = resposta;
        }

        return ultima!;
    }
}
