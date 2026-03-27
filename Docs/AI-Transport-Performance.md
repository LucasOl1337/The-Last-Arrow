# AI Transport Performance

## Objetivo

Definir uma estrategia de polling e request/response para conectar engines externas de IA com latencia operacional abaixo de 100 ms, sem travar o `FixedUpdate`.

## Recomendacao para o MVP

- `pollingIntervalMs`: 33 ms
- `requestTimeoutMs`: 60 ms
- `staleActionTimeoutMs`: 120 ms
- maximo de 1 request em voo por slot
- fallback previsivel quando nao houver resposta fresca

Esse corte produz um ciclo pratico de aproximadamente 30 Hz para decisao externa, mantendo o jogo responsivo e sem depender de round-trip por frame.

## Arquitetura recomendada

### Canal de tempo real

- snapshots curtos
- acao curta
- backend local
- dispatch assincrono
- nenhum IO bloqueando a thread principal

### Canal de controle

- consultas
- memoria
- logs
- replay curto
- diagnostico textual

## Por que nao depender de request por frame

Se o jogo rodar `FixedUpdate` a 50 Hz, cada tick tem 20 ms. Isso e apertado demais para depender de request/resposta externa em todos os frames.

O caminho correto e:

- o jogo continua rodando no proprio ritmo
- a ponte externa publica a melhor acao mais recente
- o input source aplica a ultima acao fresca recebida
- se nao houver acao fresca, entra fallback

## Backends recomendados

### MVP

- `HTTP localhost` com JSON para desenvolvimento rapido
- mock server de referencia em `tools/ai_arena_mock_server.py`

### Melhor evolucao local

- `named pipe`
- `TCP localhost`
- `MessagePack` ou payload binario compacto

### O que evitar no primeiro corte

- depender de LLM remoto decidindo frame a frame
- depender de request bloqueante na thread principal
- multiplexar logs grandes no mesmo canal de tempo real

## Politica de fallback recomendada

- usar a ultima acao continua conhecida por pouco tempo
- limpar botoes one-shot no fallback
- cair para neutro se a acao ficar velha demais

## Observabilidade minima

- estado da conexao
- ultima resposta valida
- timeouts acumulados
- latencia aproximada
- endpoint atual

## Conclusao pratica

Para ficar abaixo de 100 ms de resposta percebida:

- fazer polling em 33 ms
- timeout em 60 ms
- dispatch assincrono
- manter somente uma request em voo
- aplicar a ultima acao fresca em cache

Isso entrega jogabilidade plausivel no MVP sem exigir transporte binario logo no primeiro passo.
