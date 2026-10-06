# Tolerância a Falhas vs Resiliência em .NET

[English](README.md) | Português

Exemplo executável em .NET 10 que mostra a diferença entre dois conceitos frequentemente confundidos:

- **Tolerância a falhas**: o sistema continua funcionando quando um componente falha, idealmente sem que o usuário perceba. Mecanismo aqui: **redundância** (um balanceador que faz failover para outra instância).
- **Resiliência**: o sistema lida com falhas e degradações e se recupera delas. Mecanismo aqui: **timeout, retry com backoff exponencial e jitter, circuit breaker e fallback**, com `Microsoft.Extensions.Http.Resilience` (construído sobre o Polly v8).

## Como as camadas se encaixam

```
FreteClient            (fallback: tabela padrão de frete)
  └─ Resiliência       (timeout total → retry → circuit breaker
       │                 → timeout por tentativa)
       └─ Tolerância a falhas   (balanceador: instância A → instância B)
```

Cada tentativa do retry passa pelo balanceador, que já tenta todas as instâncias. Se tudo falhar, o cliente devolve uma resposta degradada em vez de quebrar quem chamou.

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Executar

```bash
dotnet build
dotnet run --project src/FreteResiliente --no-build
```

Não são necessários serviços externos: as instâncias do serviço de frete são simuladas em memória (`InstanciaSimulada`), cada uma com um roteiro que define o comportamento a cada chamada (ok, erro ou lenta).

## O que a saída mostra

| Cenário | O que acontece | Conceito |
|---|---|---|
| Instância A fora | O balanceador tenta a B na mesma tentativa; quem chamou recebe a resposta real | Tolerância a falhas |
| Falha transitória | Instância única falha duas vezes e o retry funciona na 3ª chamada | Resiliência (retry) |
| Tudo fora | Após 4 tentativas, o fallback devolve a tabela padrão | Resiliência (fallback) |
| Circuito aberto | A próxima chamada nem chega à dependência; o fallback responde em ~1 ms | Resiliência (circuit breaker) |
| 1ª chamada trava | O timeout de 300 ms por tentativa corta e o retry funciona | Resiliência (timeout) |

Os tempos variam a cada execução.

## Estrutura do projeto

```
src/FreteResiliente/
├── Cotacao.cs             # contrato de resposta
├── InstanciaSimulada.cs   # instância simulada (comportamento por roteiro)
├── BalanceadorHandler.cs  # tolerância a falhas: failover entre instâncias
├── FreteClient.cs         # consumidor com fallback
└── Program.cs             # pipeline de resiliência e os 5 cenários
```

## Notas para produção

- Faça retry apenas em operações idempotentes (ou use chave de idempotência).
- Sempre defina timeouts e use jitter para evitar *retry storms*.
- Um circuit breaker por dependência.
- Sinalize respostas degradadas (aqui `Origem = "tabela-padrao"`) para que logs e métricas as enxerguem.
- Instâncias redundantes que compartilham o mesmo banco ou zona caem juntas: a redundância exige falhas independentes.
- Um balanceador real também usa health checks para tirar instâncias doentes da rotação.

## Links

- [Resiliência em HTTP no .NET (Microsoft Learn)](https://learn.microsoft.com/dotnet/core/resilience/http-resilience)
- [Documentação do Polly](https://www.pollydocs.org)
