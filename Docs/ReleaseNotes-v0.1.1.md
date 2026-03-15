# The Last Arrow v0.1.1

## Destaques

- Melhorias de movimentacao dos personagens para deixar o combate mais consistente e responsivo.
- Ajustes de colisao da flecha, incluindo o tratamento das hitboxes para evitar mortes injustas fora do corpo real do personagem.
- Polimento das hitboxes de ataque melee e ultimate dos dois personagens, com anchors editaveis diretamente na cena.
- Polimento do `ProjectileOrigin` para alinhar melhor os disparos com o sprite e com o gameplay.
- Nova ultimate da Storm Dragon implementada no jogo.
- Nova ultimate da Mizu implementada no jogo com dash curto, bloqueio de flechas e repeticao da sombra.
- O ataque melee da Mizu agora consegue cortar flechas e inutiliza-las no meio do combate.
- Mapa ajustado com melhor enquadramento e zoom para combinar com a escala dos personagens e do combate.
- Animacoes de morte criadas e implementadas para os personagens jogaveis.

## Base tecnica desta versao

- Pipeline de importacao via PixelLab MCP expandido para sincronizar animacoes novas com mais seguranca.
- Sistema de spawn atualizado para respeitar melhor a posicao configurada na cena.
- Ferramentas de debug e edicao no Unity melhoradas para facilitar o ajuste fino de hitboxes e pontos de combate.
