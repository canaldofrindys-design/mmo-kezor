# Arquitetura de Kezor

## Visão geral
```
 ┌──────────────── Cliente Unity (Kezor.Client) ────────────────┐        ┌──────── Servidor (Kezor.Core) ────────┐
 │ KezorGame: entrada do jogador → comandos de texto            │  TCP   │ TcpServerHost (threads só enfileiram) │
 │ ClientState: espelho do mundo montado a partir do servidor   │◄──────►│ GameServer (1 thread, tick fixo)      │
 │ EntityView/Models/WorldBuilder/Effects: só visual            │ linhas │  · sessões, movimento, IA, combate    │
 │ HUD IMGUI: orbes, skills, chat, inventário, forja, loja       │ UTF-8  │  · drops, grupo, PvP, evento          │
 └───────────────────────────────────────────────────────────────┘        └────────────────────────────────────────┘
          ▲ só multiplayer: até no "Servidor deste PC" o cliente abre um TcpServerHost local e entra nele por TCP como os outros
```
- **Kezor.Core** (`noEngineReferences`): personagens, combate, itens, runas, forja, monstros, mapa, servidor, protocolo e
  o lado cliente da rede (`ClientState`, conexões). Compila em netstandard2.1, então roda no Unity, num `dotnet` headless e nos testes.
- **Kezor.Client**: tudo que é Unity. Não contém regra de jogo; só lê `ClientState` e envia intenções.

## Servidor autoritativo
O cliente **nunca** decide resultado. Ele manda intenções e o servidor valida tudo:
- `MOVE x z`: o servidor move o personagem na velocidade da classe a cada tick (não existe teleporte; NaN/∞ rejeitados; posição limitada ao mapa).
- `ATTACK id` / `SKILL s id`: o servidor anda até o alcance da arma/skill, checa classe, nível, energia, mana, cooldown global (0,6 s),
  regras de PvP (fora da vila, nível 6+, não atacar o próprio grupo) e rola **todos** os números aleatórios.
- `PICK id`: anda até o item; itens ficam reservados ao dono (e ao grupo dele) por 15 s e somem em 90 s.
- Loja, venda, forja e reparo só funcionam dentro da área segura da vila. Comandos GM só com `ServerOptions.AllowGm`.
- Rede: linha máx. 512 bytes, máx. 60 mensagens/s por conexão (excesso descartado).

### Loop
`GameServer.Tick(dt)`: regeneração (1 s) → intenções dos jogadores → IA dos monstros (Idle → Wander → Chase → Return, com leash de 16 u
e proibição de entrar na vila) → expiração de itens → agendador de evento → snapshots a 10 Hz.
Hosts chamam `Tick` e depois `Pump/Flush` na mesma thread: a lógica não tem locks.

## Protocolo (texto, uma mensagem por linha)
Cliente → servidor: `REGISTER conta senha · AUTH conta senha · CHARS · PLAY nome · CREATE nome GUA|ARC|PAT · LOGIN nome (convidado) · WARP [destino] · MOUNT · VAULT · VPUT slot [qtd] · VTAKE slot [qtd] · VGOLDIN n · VGOLDOUT n · VCLOSE · TRADE nome|ACCEPT|DECLINE|ADD slot|REMOVE slot|GOLD n|LOCK|CONFIRM|CANCEL · GUILD CREATE|INVITE|ACCEPT|LEAVE|KICK|PROMOTE|DEMOTE|DISBAND|INFO|WAR guilda|WARACCEPT|WARDECLINE|WARINFO · GSAY texto · SHOP OPEN nome|ADD slot preço|REMOVE slot|CLOSE|VIEW jogador|BUY jogador slot preço · RANKING GERAL|GUA|ARC|PAT|GUILDAS · MOVE x z · STOP · ATTACK id · SKILL skill id · PICK id · ADD STR|AGI|VIT|ENE n ·
EQUIP slot · UNEQUIP RightHand|LeftHand|Helm|Armor|Pants|Gloves|Boots · USE slot · RUNE runa alvo · FORGEPREVIEW a,b,c · FORGE a,b,c ·
BUY itemId [qtd] · SELL slot · REPAIR · REBIRTH · SAY texto · PARTY INVITE nome|ACCEPT|LEAVE · STATS · LOOK · INV · HELP · GM ...`

Servidor → cliente:
| Mensagem | Conteúdo |
|---|---|
| `OK …` / `ERR …` / `FAIL …` | resposta a cada comando, na mesma ordem (o cliente casa com uma fila de callbacks) |
| `@TRADE parceiro\|ouro\|travas\|…\|M;…\|T;…` / `@TRADE CLOSE` | estado da troca (os dois lados) |
| `@GUILD nome\|cargo\|membro;cargo;online…` / `@GUILD NONE` | guilda do personagem |
| `@PSHOP dono\|título\|aberta\|slot;preço;item…` | vitrine de uma loja pessoal |
| `@RANK tipo\|pos;nome;…` | top 10 do ranking pedido |
| `@WELCOME id mapa` | entrou no mundo ou trocou de mapa (o cliente refaz o cenário) |
| `@VAULT ouro\|I;slot;item…` / `@VAULT CLOSE` | conteúdo do baú enquanto está aberto (perto da guardiã) |
| `@S t\|P;…\|M;…\|D;…` | snapshot 10 Hz das entidades **do mesmo mapa** num raio de 45 u (AOI). P = jogador (com o campo de aparência), M = monstro, D = item no chão |
| `@ME …` | status do próprio personagem (só quando muda) |
| `@INV ouro\|I;slot;item…\|E;slot;item…` | inventário/equipamento (quando a versão muda) |
| `@EV TIPO;…` | eventos: `HIT`, `FX` (área), `DIE`, `LVL`, `EXP`, `GOLD`, `CHAT`, `SYS`, `PARTY`, `INVITE`, `EVENT` |

Números sempre em **cultura invariante** (ponto decimal), testado com `pt-BR`. Textos livres passam por `Protocol.Clean` (remove `;` `|` quebras).

## Cliente
- `KezorGame` se cria sozinho no Play (`RuntimeInitializeOnLoadMethod`), constrói o mundo, mostra a tela de entrada e conecta.
- `ClientState.Process(linha)` atualiza entidades/inventário/log; `SyncViews` cria/destrói `EntityView` e interpola posições.
- Clique no chão é resolvido com *ray marching* sobre a mesma função de altura do terreno (`WorldBuilder.Height`), sem colliders.
- Visual: um único material/shader (`Kezor/LowPoly`), malhas flat-shaded com cor por vértice; alfa < 0,5 = emissivo.
  Luz e névoa são globais de shader, então funciona igual no Built-in e no URP.

## Mapas, rotas e persistência
- `MapDefinition.CreateAll()` cria os mapas (Vale, Pântano, Cripta). Personagens, monstros e itens no chão têm `MapId`; `GameServer.MapOf(id)`.
  Colisão: muralha, obstáculos circulares e, nas masmorras, retângulos de chão (`Floors`). O cliente desenha a partir dos mesmos dados.
- `Pathfinder`: A* numa grade de 1 m por mapa (calculada uma vez), 8 vizinhos sem cortar quina e caminho suavizado; usado quando a
  linha reta do jogador bate em algo. Portais e `WARP` chamam `Transfer`, que manda `@WELCOME` com o mapa novo.
- Contas: `Account` (hash PBKDF2-SHA256, até 3 personagens, baú). `IAccountRepository` + `ICharacterRepository`:
  em memória (local/testes) ou `JsonFileRepository` (pastas `accounts/` e `characters/`, escrita atômica via `.tmp`). JSON próprio
  (`MiniJson`), porque o Core não usa pacotes. Salva ao desconectar, a cada `AutosaveSeconds` e em `SaveAll()` no desligamento.

## Troca e guildas
- Troca: estado por par de sessões (`Trade`), oferta guarda a referência do item e o espaço; antes de trocar, `ReferenceEquals` em cada
  espaço garante que ninguém duplica itens movendo-os no meio. `TickTrades` destrava quando um item oferecido muda e cancela por distância.
- Guildas: `Guild` (membros por nome de personagem e cargo) em `IGuildRepository` (memória ou `guilds/*.json`), carregadas ao iniciar.
  O snapshot leva o nome da guilda de cada jogador.
- IA: monstros usam `StepMonster` (A* + `TryStep`) para perseguir e voltar.

## Loja, ranking e guerra
- Loja pessoal por sessão (`PersonalShop`), anúncios com a referência do item e o espaço; `TickShops` derruba anúncios de itens que mudaram.
- `Ranking`: tabela em memória por nome de personagem, salva em `IRankingRepository` (`ranking.json`) junto com o autosave.
- `GuildWar`: placar por guerra; `OnPlayerKilled` chama `CountWarKill` (ponto em vez de infâmia); `TickWars` encerra e grava o resultado.

## Próximos passos (para sair do vertical slice)
1. **Persistência**: feita em JSON com contas PBKDF2; falta trocar por banco (PostgreSQL/SQLite) para muitos jogadores e pôr TLS na conexão.
2. **Protocolo binário** com opcodes e delta de snapshots; *client-side prediction* do próprio movimento.
3. **Vários mapas**: feito num processo só; para escalar, um processo por mapa atrás de um gateway.
4. Guildas, troca, loja pessoal, ranking e guerra de guildas: feitos. Falta cerco de castelo/territórios.
5. Inventário em grade W×H, conjuntos de itens, mais classes e skills, mais eventos.
6. Trocar os modelos procedurais por modelos feitos no Blender (via MCP quando disponível), mantendo os `Rig`.
