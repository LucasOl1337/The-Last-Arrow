# AI Update Cycle

Toda IA que atuar neste projeto deve repetir este ciclo.

## Entrada obrigatoria
1. Ler `Docs/ContextAndAiGuide/CURRENT_CONTEXT.md`.
2. Ler a entrada mais recente em `Docs/ContextAndAiGuide/Daily/`.
3. Se houver pacote aberto, ler o arquivo correspondente em `Docs/ContextAndAiGuide/Packages/`.

## Execucao obrigatoria
1. Criar um novo arquivo datado no mesmo dia usando o formato `YYYY-MM-DD-slug.md`.
2. Registrar:
   - objetivo da sessao
   - arquivos alterados
   - decisoes tomadas
   - riscos conhecidos
   - proximos passos
3. Se o trabalho gerar handoff, patch, manifesto ou corte de integracao, criar ou atualizar um arquivo em `Packages/`.
4. Atualizar `CURRENT_CONTEXT.md` apontando para a entrada mais nova.
5. Manter `INDEX.md` em ordem cronologica reversa, com o item mais recente no topo.

## Regras
- Nao sobrescrever contexto anterior sem registrar o novo estado em um arquivo datado.
- Nao depender de memoria implícita da conversa.
- Nao fechar sessao tecnica sem deixar proximo passo explicito.
- Se o merge ainda nao for seguro, registrar o bloqueio no pacote de integracao do dia.

## Convencoes de nome
- Diario: `Daily/YYYY-MM-DD-slug.md`
- Pacote: `Packages/YYYY-MM-DD-slug.md`
- Slug curto, em minusculo, com hifens.
