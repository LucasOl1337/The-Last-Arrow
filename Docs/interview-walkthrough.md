# Interview Walkthrough

## Pitch curto

"Eu usei a migracao do Godot para levantar requisitos de gameplay, mas a base final foi organizada como um projeto Unity de producao. Separei dados por personagem, runtime de gameplay, apresentacao visual e ferramentas leves de editor para deixar o codigo sustentavel e facil de manter."

## Como explicar a arquitetura em 3 blocos

### 1. Dados

- `CharacterDefinition` e `ArenaDefinitionAsset` concentram configuracao de gameplay.
- Cada personagem tem pasta propria em `Assets/ProjectPVP/Characters`.
- Isso deixa animacoes, sprites e configs acessiveis sem depender de pipeline externo.

### 2. Editor tooling

- `ProjectPvpEditorPlayModeSetup` garante que o Play entre na cena certa.
- `ProjectPvpInputManagerInstaller` instala os eixos de gamepad necessarios.
- `ProjectPvpPlayableValidator` verifica se a cena principal esta consistente.

### 3. Runtime

- `KeyboardPlayerInputSource` traduz teclado/gamepad em `PlayerInputFrame`.
- `PlayerController` concentra locomocao e combate.
- `ProjectileController` cuida da fisica do projetil.
- `MatchController` resolve round loop e scoreboard.

## Se perguntarem "por que nao portar 1:1?"

Resposta boa:

"Porque a engine mudou. Se eu tentasse copiar node por node, eu traria complexidade acidental do Godot para o Unity. Preferi preservar os requisitos de gameplay, mas reconstruir a base usando os componentes e o fluxo natural do Unity."

## Se perguntarem "onde mora a logica principal?"

- Input local: `Assets/ProjectPVP/Scripts/Runtime/Input`
- Combate e locomocao: `Assets/ProjectPVP/Scripts/Runtime/Gameplay/PlayerController.cs`
- Projetil: `Assets/ProjectPVP/Scripts/Runtime/Gameplay/ProjectileController.cs`
- Round flow: `Assets/ProjectPVP/Scripts/Runtime/Match/MatchController.cs`

## Se perguntarem "o que exatamente foi recriado do Godot?"

- movimento lateral
- pulo e wall-jump
- dash com cooldown e parry window
- mira segurando o botao e disparo ao soltar
- projetil com gravidade progressiva
- stick no cenario e coleta de flecha
- melee
- stomp por cima
- respawn e contagem de rounds

## Se perguntarem "como voce garante organizacao?"

Resposta boa:

"Eu organizei os assets por personagem, separei runtime de editor tooling e deixei os dados em `ScriptableObjects`. Isso facilita manutencao, debug no Inspector e onboarding de outras pessoas."

## Se perguntarem "qual foi o maior risco da migracao?"

Resposta boa:

"O maior risco era terminar com um prototipo que ate abre no Unity, mas continua preso a um pipeline de migracao confuso. Por isso eu transformei a base em assets e scripts estaveis do Unity, com validacao, HUD de debug e organizacao por personagem."

## Como demonstrar ao vivo

1. Mostrar `Bootstrap.unity`.
2. Mostrar a pasta `Assets/ProjectPVP/Characters`.
3. Mostrar `CharacterDefinition`.
4. Mostrar `PlayerController`.
5. Dar Play e explicar o HUD.

## Limites atuais para falar com honestidade

- skills especiais ainda nao estao com paridade completa
- camada visual ainda esta abaixo do runtime de mecanicas
- ainda existe polimento fino de animacao, input e frame feel a fazer
