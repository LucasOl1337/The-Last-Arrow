# Combat Playtest Checklist

## Objetivo

Checklist manual para o slice atual de combate 2D em Unity, inspirado na skill `game-playtest` do plugin `Game Studio`.

## Pre-flight

1. Abrir a cena `Bootstrap`.
2. Garantir que os slots carregaram personagens.
3. Confirmar que o Game View recebeu foco.
4. Ligar Gizmos se o teste envolver hitbox, hurtbox ou probes.
5. Usar `F3` quando for preciso validar colisao e leitura de alcance.

## Smoke pass obrigatorio

1. Mover para esquerda e direita com os dois slots.
2. Pular, cair e testar wall slide.
3. Executar dash no chao e no ar.
4. Atirar parado, em movimento e com alvo em altura diferente.
5. Executar melee em curta distancia.
6. Executar ultimate e observar janela de windup, deslocamento e recovery.
7. Forcar reset de round e validar respawn.

## Leitura de combate

- da para identificar quem esta atacando sem olhar debug?
- o alcance do melee parece justo com o sprite e com a hitbox?
- o tiro sai de um ponto coerente com a pose?
- dash e ultimate continuam legiveis quando o fundo esta em movimento?
- os personagens mantem silhueta clara em proximidade?

## Feedback de impacto

- o hit confirma impacto suficiente?
- o alvo responde com stun, knockback ou outro feedback coerente?
- o ataque pesado parece mais pesado do que o leve?
- audio, flash e movimento reforcam a leitura em vez de poluir?

## HUD e playfield

- o centro da tela continua limpo durante a luta?
- informacao critica cabe em clusters compactos nas bordas?
- debug detalhado fica escondido por padrao?
- nao existe painel permanente bloqueando leitura de salto, dash ou spacing?

## Movimento e espacamento

- aceleracao e frenagem passam sensacao de controle?
- wall jump cria reposicionamento previsivel?
- dash gera vantagem clara sem apagar o risco?
- knockback abre ou fecha espaco de forma legivel?

## Personagem e identidade

- Mizu parece mais agil e tecnico?
- Storm Dragon parece mais pesado e forte?
- as diferencas aparecem no timing, deslocamento e impacto, nao so no visual?

## Quando abrir finding

Registrar finding quando houver:

- perda de legibilidade do combate
- acao com alcance visual e alcance real divergentes
- HUD cobrindo informacao de leitura espacial
- feedback de impacto fraco ou enganoso
- reset, respawn ou input quebrando depois de troca de round

## Formato do finding

- o que o jogador viu
- como reproduzir
- porque importa
- subsystem dono provavel

