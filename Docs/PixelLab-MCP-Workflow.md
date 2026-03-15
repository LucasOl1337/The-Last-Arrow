# PixelLab MCP no The Last Arrow

Status validado em 2026-03-14.

## O que foi confirmado

- O PixelLab tem MCP oficial em `https://api.pixellab.ai/mcp`.
- A API v2 oficial tambem esta ativa em `https://api.pixellab.ai/v2/...`.
- As operacoes de criacao e animacao sao assincronas: criam um job, depois voce consulta status.
- O token configurado para esta maquina esta valido e a conta tinha `2000` geracoes de assinatura disponiveis durante a validacao.
- Este Windows nao tinha `node`/`npm` no PATH no momento da pesquisa, entao a melhor integracao no Codex aqui e MCP HTTP nativo, sem `npx mcp-remote`.
- A `Mizu` ja ficou configurada com `pixelLabCharacterId` e aliases iniciais para sync direto no inspector do Unity.

## Configuracao preparada

- O workspace `C:\Users\user\Desktop\The Last Arrow` foi marcado como `trusted` no arquivo global `C:\Users\user\.codex\config.toml`.
- O servidor MCP `pixellab` foi configurado no mesmo arquivo usando `url = "https://api.pixellab.ai/mcp"`.
- O header de autenticacao foi movido para a variavel de ambiente de usuario `PIXELLAB_AUTH_HEADER`, evitando deixar o bearer token exposto em texto puro no TOML.

Reinicie o Codex para o processo novo enxergar a variavel de ambiente e carregar o MCP.

## Fit com o projeto atual

- Projeto Unity: `6000.3.11f1`.
- Pipeline atual: personagens ficam em `Assets/ProjectPVP/Characters/<NomeDoPersonagem>/`.
- Rotacoes estaticas ficam em `Rotations/` com nomes como `east.png`, `north-east.png`, `south.png`.
- Animacoes ficam em `Animations/<acao>/<direcao>/frame_###.png`.
- O sincronizador atual aceita apenas direcoes laterais para animacoes: `east/west` ou `left/right`.
- `north`, `south` e diagonais podem existir no ZIP do PixelLab, mas hoje nao entram no runtime animado deste projeto.

## Formato real do ZIP do PixelLab

Estrutura confirmada a partir de um export real da sua conta:

```text
rotations/south.png
rotations/east.png
rotations/west.png
rotations/north.png
rotations/south-east.png
rotations/north-east.png
rotations/north-west.png
rotations/south-west.png
animations/<nome-da-animacao>/<direcao>/frame_000.png
metadata.json
```

## Workflow recomendado daqui pra frente

1. No Codex, inclua o link `https://api.pixellab.ai/mcp/docs` no prompt quando quiser dar contexto adicional ao modelo.
2. Gere o personagem com `view="side"` e `n_directions=8` para manter boas rotacoes base.
3. Ao pedir animacoes, use `animation_name` igual as chaves do projeto:
   - `idle`
   - `aim`
   - `walk`
   - `running`
   - `shoot`
   - `dash`
   - `jump_start`
   - `jump_air`
   - `melee`
   - `ult`
4. Sempre que possivel, peca animacoes laterais primeiro.
   - O projeto consome `east/west` automaticamente.
5. Baixe o ZIP final do personagem.
6. No Unity, selecione o `CharacterDefinition` do personagem alvo.
7. Se o personagem tiver `pixelLabCharacterId`, use `Sync From PixelLab` no inspector ou `ProjectPVP/Characters/Sync Selected Character From PixelLab`.
8. Se voce ja baixou o ZIP manualmente, use `ProjectPVP/Characters/Import PixelLab ZIP To Selected Character`.
9. O importador copia `Rotations/`, normaliza nomes de acao para o runtime, copia animacoes laterais, reotimiza imports e executa o rebuild dos clips.

## Regras praticas para prompts

- Use nomes de animacao curtos e exatos, sem frases longas, se quiser plugar direto no gameplay.
- Deixe a descricao visual no `action_description`, nao no `animation_name`.
- Exemplo bom:

```text
animate_character(
  character_id="...",
  template_animation_id="breathing-idle",
  animation_name="idle",
  action_description="calm breathing with bow ready",
  directions=["east", "west"]
)
```

- Exemplo ruim para este projeto:

```text
animation_name="custom-BOW AND ARROW AIMING, WITH HER YUMI BOW"
```

Esse tipo de nome importa o asset, mas nao conversa automaticamente com as actions do runtime.

## Limitacoes atuais

- O runtime ainda nao usa animacoes `north`, `south` ou diagonais.
- O sincronizador rebuilda apenas direcoes laterais.
- Pastas de animacao sem match seguro com as actions do runtime sao ignoradas no sync automatico.
- Downloads de ZIP sao endpoint HTTP da API v2; criacao/consulta de assets pode ser feita por MCP ou por API.

## Fontes oficiais usadas

- PixelLab MCP docs: <https://api.pixellab.ai/mcp/docs>
- PixelLab API v2 docs: <https://api.pixellab.ai/v2/llms.txt>
- OpenAI Codex MCP config reference: <https://developers.openai.com/codex/configuration/mcp>
