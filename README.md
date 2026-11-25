# Kezor — Crônicas de Brasalume

ARPG online isométrico de **fantasia sombria low poly**, feito em **Unity (C#)** com **servidor autoritativo**.
Mecânicas clássicas do gênero (atributos por ponto, renascimento, refino +0~+15 com runas, forja com chance de falha,
itens Ascendentes, combos, PvP, grupo, evento de invasão), com **mundo, classes, itens, monstros e arte 100% originais**.

> Este *vertical slice* tem 3 mapas (Vale de Brasalume com a vila e o Bosque Cinzento, Pântano de Turfa Negra e a masmorra Cripta de Vidro Negro), 7 classes, 17 monstros (incluindo 2 ecos invocáveis),
> ~35 itens, 4 runas, 2 receitas de forja e 1 evento. Tudo é gerado por código (malhas low poly procedurais), sem assets externos.

## Como jogar no Unity
1. Abra a pasta `C:\MuProtoUnity` no **Unity Hub** (Add ▸ Add project from disk). Testado para Unity **2021.3 LTS ou mais novo** (inclusive Unity 6).
2. Abra qualquer cena (a vazia serve) e aperte **Play**. O jogo se monta sozinho (`KezorGame` se cria automaticamente).
   - Opcional: menu **Kezor ▸ Criar cena do jogo (cliente)** salva `Assets/Scenes/Kezor.unity` e coloca no Build Settings.
3. Escolha nome e classe, deixe **“Jogar local”** marcado e clique **Entrar no mundo**.

Se a Unity pedir para mudar o *Active Input Handling*, deixe em **Input Manager (Old)** ou **Both** (o cliente usa `UnityEngine.Input`).

### Controles
| Ação | Comando |
|---|---|
| Andar | clicar/segurar no chão |
| Atacar (contínuo) | clicar no monstro |
| Skill | teclas **1-6** escolhem, **botão direito** usa no alvo (buffs, estandartes e ecos não precisam de alvo) |
| Poções HP / MP | **Q** / **W** |
| Montar / desmontar | **R** (com montaria equipada) |
| Viajar | portais fora do portão leste ou `/warp` (lista), `/warp PANTANO`, `/warp CRIPTA`, `/warp VALE` |
| Baú | fale com **Ilsa, a Guardiã** na praça; arraste itens entre o baú e a mochila |
| Troca | **T** com o mouse sobre outro jogador (ou `/trade Nome`); travar e depois confirmar |
| Guilda | **G** abre a guilda; fundar com **Hadrik** na praça; chat da guilda `/g texto`; guerra: `/guild war NomeDaGuilda` |
| Loja pessoal | **B** monta a barraca (só na vila); clique num vendedor para ver a vitrine |
| Ranking | **K** (geral, por classe e de guildas) |
| Pegar item | clicar no item ou **Espaço** |
| Janelas | **C** personagem · **I** inventário · **P** grupo · **G** guilda · **K** ranking · **M** mapa e viagem · **O** opções · **H** ajuda · **Esc** fecha |
| Chat | **Enter** (`/p texto` = grupo; `/comando` envia um comando cru, ex.: `/gm level 50`) |
| Câmera | roda = zoom · botão do meio arrastando = girar |

NPCs da vila: **Mercadora Tália** (loja/venda), **Ferreiro Bram** (Forja Arcana e reparo), **Sentinela Orvo**.

### Classes
| Classe | Papel |
|---|---|
| Guardião Rúnico | linha de frente, combo de skills |
| Arcanista do Véu | magia em área |
| Patrulheiro Selvagem | arco, à distância |
| **Lâmina Arcana** | híbrido: força e energia somam no mesmo golpe |
| **Comandante do Estandarte** | lança e estandartes com auras para o grupo; ordens em área |
| **Invocadora de Ecos** | até 2 ecos que lutam por ela; maldições de lentidão e dano contínuo |
| **Punho de Brasa** | manoplas, ataques rápidos e o Fluxo de Brasa (5 golpes seguidos = explosão) |

Detalhes, números e o teste de equilíbrio em `docs/DESIGN.md`.

### Interface
Interface em **UI Toolkit** com estilo próprio (carvão, metal envelhecido e brasa), guia em `docs/UI_GUIDE.md`:
HUD com orbes animadas, barra de skills com recarga radial, buffs com timer, quadro do alvo, minimapa redondo, chat com abas e
toasts; 14 janelas arrastáveis (posição salva) com tooltip de item com comparação e arrastar/soltar; telas de login e criação com
o herói 3D girando num pedestal; tela de carregamento entre mapas. Escala automática de 720p a 4K e opção de escala (O).
Arte da interface e ícones são gerados em código: menus **Kezor ▸ Gerar arte da UI** e **Kezor ▸ Gerar ícones**.

### Modos de conexão
- **Jogar local**: servidor embutido no cliente (mesmo código do dedicado). Comandos GM liberados para testar:
  `/gm level 50`, `/gm gold 10000000`, `/gm runes`, `/gm event` (inicia a Invasão), `/gm heal`, `/gm item <id> [nível]` (ex.: `/gm item 302 9` = Asas de Brasalume +9).
- **Hospedar na rede (LAN)**: abre a porta 55901 no seu PC e você joga; amigos usam “Conectar a um servidor” com o seu IP.
- **Conectar a um servidor dedicado**: veja abaixo.

## Servidor dedicado
- **Sem Unity** (recomendado): `cd Build && dotnet run --project Headless -- 55901` (adicione `--gm` para liberar comandos GM).
  O servidor exige **conta**: no cliente, escolha “Conectar a um servidor dedicado”, marque “Criar conta nova” na primeira vez e depois
  crie até 3 personagens. Os saves ficam em JSON na pasta `data/` (mude com `--data <pasta>`; `--guest` deixa jogar sem conta).
- **No Unity**: menu **Kezor ▸ Criar cena do servidor dedicado** (usa `ServerBootstrap`) e gere um build *Dedicated Server*.
- Dá para testar na mão com `telnet`/`nc localhost 55901` e digitar `HELP`.

## Testes
- `cd Build && dotnet test Tests/Kezor.Tests.csproj` (109 testes: inventário em grade (encaixe, cheio, mover, troca, baú, equipar, migração), interface (buffs e recargas no protocolo), 7 classes e simulação de DPS, estandartes, ecos, maldições, Fluxo de Brasa, combate, itens, runas, forja, conjuntos, IA com A*, troca, guildas, loja pessoal, ranking, guerra de guildas, asas, montarias, mascotes, aparência, mapas e portais, baú, contas e saves, movimento e colisão, IA, drops, grupo, PvP, evento, protocolo TCP e cultura pt-BR).
- No Unity: **Window ▸ General ▸ Test Runner ▸ EditMode**.
- `Build/ClientCheck` compila o cliente fora do Unity (referências do UnityEngine via NuGet) para pegar erros cedo.

## Estrutura
```
Assets/
  Scripts/Core/      regras do jogo em C# puro (sem UnityEngine): roda no cliente, no servidor dedicado e nos testes
    Characters/ Combat/ Items/ World/ Server/ Net/
  Scripts/Client/    Unity: KezorGame (entrada/rede), HUD IMGUI, WorldBuilder, Models, EntityView, Effects, ServerBootstrap
  Scripts/Editor/    menu "Kezor"
  Shaders/           Kezor/LowPoly (cor por vértice + luz flat + névoa; funciona no Built-in e no URP)
  Tests/EditMode/    testes NUnit
Build/               csproj para servidor dedicado, testes e checagem do cliente sem Unity
docs/                ARQUITETURA.md e DESIGN.md
_old_prototype/      protótipo anterior (MuProto), guardado fora de Assets para referência
```

## Arte
Os modelos são **low poly feitos no Blender**: a fonte é `Art/Kezor_Models.blend`, gerada pelos scripts de
`Art/scripts/` (`kezor_lib.py`, `kezor_humans.py`, `kezor_monsters.py`, `kezor_env.py`), e `kezor_export.py`
exporta os FBX para `Assets/Resources/Models/<Classes|Monsters|Npcs|Environment>/`. As cores vão na **cor de vértice**
(alfa < 0,5 = emissivo), e todos os modelos usam o material único `Kezor/LowPoly`.
Prévias: `Art/previews/`. Detalhes de eixos, hierarquia e nomes: `Art/PROGRESSO.md`.

- `Scripts/Client/ModelLibrary.cs` carrega os prefabs com `Resources.Load` e monta o `Rig` pelos nomes dos filhos
  (`Body` → `Head`, `ArmL`, `ArmR`, `LegL`, `LegR`, `Leg0..N`, `Orb0..N`). `StaticBatch` combina instâncias de cenário por chunk.
- `Scripts/Editor/ModelImportPostprocessor.cs` configura a importação: escala 1, hierarquia preservada,
  sem animação nem materiais, e Read/Write ligado só em Environment.
- **Fallback procedural:** se um FBX não existir, ou se o cenário não tiver Read/Write, `Models.cs` e `WorldBuilder.cs`
  montam o modelo antigo com `MeshBuilder` (`Scripts/Client/LowPoly.cs`). Terreno, lanternas, barraca, forja,
  itens no chão e efeitos continuam procedurais. `ModelLibrary.Enabled = false` força tudo procedural.

Mais detalhes: [docs/ARQUITETURA.md](docs/ARQUITETURA.md) · [docs/DESIGN.md](docs/DESIGN.md)
