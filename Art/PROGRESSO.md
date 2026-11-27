# Kezor: modelos low poly (Blender → Unity)

Fonte: `Art/Kezor_Models.blend` (coleções Classes, Npcs, Monsters, Environment e PreviewStage, que tem só chão e luzes para os renders).
Tudo é gerado por script e dá para refazer:
- `Art/scripts/kezor_lib.py`: materiais (uma cor por material, emissão para o que brilha) e o construtor de malha flat-shaded.
- `Art/scripts/kezor_humans.py`: classes, NPCs, Ossada, Cria e Arauto.
- `Art/scripts/kezor_monsters.py`: Lodo, Lobo, Aranha e Golem.
- `Art/scripts/kezor_env.py`: cenário.
- `Art/scripts/kezor_export.py`: exporta todos os FBX.

Para recriar, execute no Blender os .py na ordem acima, no mesmo namespace (`exec`), e chame cada função passando a coleção.

## Eixos e hierarquia
- No Blender a frente do modelo é **-Y** (a vista Front mostra o rosto), a esquerda do personagem é +X e a origem fica nos pés.
- Os FBX saem com Forward -Z / Up Y e escala FBX All, então no Unity a escala é 1 e a frente fica em **+Z**.
- O "Apply Transform" (bake_space_transform) do exportador do Blender grava rotação de 90° errada nos objetos filhos. Por isso
  o `kezor_export.py` converte malhas e pivôs para Y-up à mão antes de exportar. Resultado: no FBX todas as partes têm rotação 0
  e escala 1, e o pivô fica na articulação. Conferi isso lendo os 21 arquivos.
- Hierarquia no FBX: `Body` (origem nos pés) → `Head`, `ArmL`, `ArmR`, `LegL`, `LegR`. Lobo: Leg0..Leg3 (0 = dianteira esq., 1 = dianteira dir.,
  2 = traseira esq., 3 = traseira dir.). Aranha: Leg0..Leg7 (0–3 lado esquerdo, 4–7 direito, da frente para trás). Arauto: Orb0..Orb2 (orbes).
  Lodo: só `Body`. Cenário: uma malha com o nome do modelo; a Fonte tem o filho `Gem` (gema, para girar/flutuar).
- Os materiais se chamam `K_<Nome>` e são compartilhados entre modelos. Não há texturas.

## Modelos
| Modelo | FBX | Tris |
|---|---|---|
| Guardião Rúnico | Classes/GuardiaoRunico.fbx | 644 |
| Arcanista do Véu | Classes/ArcanistaDoVeu.fbx | 784 |
| Patrulheiro Selvagem | Classes/PatrulheiroSelvagem.fbx | 764 |
| Mercadora Tália | Npcs/MercadoraTalia.fbx | 548 |
| Ferreiro Bram | Npcs/FerreiroBram.fbx | 512 |
| Sentinela Orvo | Npcs/SentinelaOrvo.fbx | 636 |
| Lodo Musgoso | Monsters/LodoMusgoso.fbx | 272 |
| Lobo Cinzento | Monsters/LoboCinzento.fbx | 384 |
| Aranha Tecelã | Monsters/AranhaTecela.fbx | 556 |
| Ossada Errante | Monsters/OssadaErrante.fbx | 684 |
| Golem de Basalto | Monsters/GolemDeBasalto.fbx | 632 |
| Cria das Cinzas | Monsters/CriaDasCinzas.fbx | 526 |
| Arauto das Cinzas | Monsters/ArautoDasCinzas.fbx | 854 |
| Casa de enxaimel | Environment/CasaEnxaimel.fbx | 880 |
| Torre de portão | Environment/TorrePortao.fbx | 364 |
| Segmento de muralha (4 m em X, lado de fora = frente) | Environment/MuralhaSegmento.fbx | 264 |
| Fonte de Brasas (+ Gem) | Environment/FonteDeBrasas.fbx | 432 |
| Pinheiro | Environment/Pinheiro.fbx | 68 |
| Árvore de copa | Environment/ArvoreCopa.fbx | 116 |
| Árvore morta | Environment/ArvoreMorta.fbx | 156 |
| Pedra | Environment/Pedra.fbx | 40 |

Pré-visualizações: `Art/previews/Classes.png`, `Npcs.png`, `Monsters.png` e `Environment.png`.

## Integração no Unity (etapa 2)
FBX em `Assets/Resources/Models/<Categoria>/` (o caminho antigo `Assets/Models` foi movido para cá). A tabela acima usa os
caminhos relativos a essa pasta. O kezor_export.py também grava a cor de vértice `Col` (sRGB do material; alfa < 0,5 nos materiais emissivos).

Feito e compilado sem erros, conforme o Logs/Editor.log de 21:49 (Kezor.Client, Kezor.Editor e os 21 FBX importados):
- `Assets/Scripts/Editor/ModelImportPostprocessor.cs`: escala 1, preserveHierarchy, sem animação/câmeras/luzes/materiais,
  Read/Write só em Environment. Conferido nos .meta.
- `Assets/Scripts/Client/ModelLibrary.cs`: `ModelLibrary` (Resources.Load com cache; troca os materiais pelo Kezor/LowPoly;
  `Rig()` monta o Rig por nome; Height pelos bounds), `StaticBatch` (combina malhas legíveis com TRS e tint) e `Spinner` (gema da fonte).
- `Models.cs`: Player, Npc e Monster tentam o FBX primeiro (mapas ClassModel, NpcModel e MonsterModels 1, 2, 3, 4, 5, 50, 51);
  Lodo com Bouncy, Arauto com Hover e orbes em ExtraLegs. Se não houver FBX, usa o procedural de antes. A arma do jogador é a do FBX
  da classe; quando a arma muda, o rig é recriado com o mesmo FBX.
- `WorldBuilder.cs`: muralha (28 segmentos, portões simétricos), 8 torres (escala 0,8, porta para a praça), casas (escala 0,78–0,90,
  mesma rotação do código), Fonte de Brasas instanciada com Spinner na gema, e natureza (pedra, pinheiro, copa, morta) combinada por
  chunk 4×4 com a mesma sequência aleatória de posição/rotação/escala e tint mais frio longe da vila. Fallback procedural por tipo.
- README.md, seção Arte, atualizada.

## Teste no jogo pelo UnityMCP (etapa 3, 21/11/2026)
UnityMCP ligado via HTTP em `127.0.0.1:8090`. Cena `Assets/Scenes/Kezor.unity` criada pelo menu **Kezor ▸ Criar cena do jogo (cliente)**,
Play em modo local (servidor embutido/loopback). Unity 6000.6.7f1.

Conferido no jogo:
- Console sem erros nem avisos do projeto.
- Personagem aparece, anda, ataca, sobe de nível e pega itens; monstros (Lodo, Lobo, Aranha, Ossada) e os 3 NPCs visíveis com nome.
- Frente dos FBX em **+Z**: personagem, NPCs e lobo vistos de frente com a câmera em +Z (viseira, escudo na mão esquerda, focinho).
- Muralha circular, 8 torres com a porta para a praça, Fonte de Brasas com a gema girando, natureza por chunk.
- HUD IMGUI: painel do personagem, minimapa, orbes de HP/MP, barra de skills, exp, chat, janela de ajuda, números de dano.

Corrigido:
- **Casas de costas para a praça**: a rotação apontava a frente (+Z, porta) para fora. Agora `yaw = -a - 90` (porta para o centro).
- **Sem colisão**: o jogador atravessava muralha, torres, casas e fonte. O `MapDefinition` agora guarda muralha (raio 16, 1,4 m),
  torres, casas, fonte e postes como obstáculos; o servidor bloqueia o passo, desvia girando a direção (contorna círculos)
  e atravessa a muralha só pelos portões (`Steer` escolhe o portão). O `WorldBuilder` lê casas e torres desses mesmos dados.
- **Teste TcpEndToEnd falhava só no Unity**: os `await` do `TcpServerHost` voltavam para a thread principal (contexto de sincronização
  da Unity), presa no teste. Agora usam `ConfigureAwait(false)`; no modo Host isso também tira a latência de um frame.
- **Chat cortava mensagens longas** e cobria o orbe de HP: agora quebra linha e fica acima dos orbes.
- Avisos do compilador: `FindObjectsSortMode` obsoleto no Unity 6.6 (com `#if`) e campo `Rig` não serializável.
- Testes: 43/43 no Unity (EditMode) e no `dotnet test`. Novo teste `WallBlocksAndGatesRoute`; `MovementIsServerSideAndSpeedLimited`
  agora parte da linha do portão leste (antes andava 50 m em linha reta através da muralha).

Capturas: `Art/previews/ingame_cidade.png` (praça com HUD), `ingame_combate.png` (luta com Lodo, dano e barra de vida),
`ingame_visao_geral.png` (vila de cima, sem HUD).

## Ajustes da vila (etapa 4, 21/11/2026)
- **Casas fora das estradas:** 7 casas em ângulos fixos (62°, 116°, 154°, 206°, 240°, 300°, 334°), todas a 26° ou mais do eixo
  de um portão e longe da barraca da Tália e da forja do Bram. Os 8 postes de lanterna giraram 22,5° para sair do meio das ruas.
  Tudo fica no `MapDefinition` (colisão do servidor) e o `WorldBuilder` desenha a partir dos mesmos dados.
  Novo teste `EveryGateHasAClearRoadToTheSquare`: faixa de 2,4 m livre do lado de fora de cada portão até a praça, e o herói
  atravessa em linha reta no tempo da distância.
- **Câmera não perde o herói:** o shader `Kezor/LowPoly` ganhou `_Occluder` e o global `_KezorSeeTarget` (posto pela `IsoCamera`).
  Nas malhas da vila e da natureza (material `KezorLook.OccluderMaterial`), o que fica num cilindro de ~3 m entre a câmera e o
  herói vira pontilhado (Bayer 4×4, até ~90% recortado), sem ordenar transparência e funcionando nas malhas combinadas.
  Personagens, monstros e terreno não são recortados.
- **Telhados variados:** o vermelho do telhado da CasaEnxaimel é trocado por vértice (vermelho, ardósia, musgo, telha queimada).
- Testes: 44/44 no Unity e no `dotnet test`.

Capturas: `Art/previews/ingame_visao_geral_v2.png` (estradas livres, telhados), `ingame_oclusao_v2.png` (herói atrás da muralha
sul, visto pelo recorte), `ingame_cidade_v2.png` (praça com HUD).

## Equipamento visível, asas, montarias e mascotes (etapa 5, 21/11/2026)
Tudo original de Kezor (nomes, modelos e itens). Script novo: `Art/scripts/kezor_equip.py` (rodar depois de lib, humans e monsters;
`build_all()` cria os modelos e `export_all(only=...)` do kezor_export exporta só eles). 53 modelos, salvos no `.blend`:
- **Corpos base sem equipamento**: `Classes/<Classe>Base.fbx` (cabelo à mostra, roupa simples da cor da classe).
  O cliente usa o modelo antigo (já armado) só se o Base não existir.
- **Peças** em `Equip/`: conjuntos Couro, Escamas e Rúnica (elmo, armadura, calças, luvas e botas, com L/R), Véu (capuz, túnica,
  sandálias), 13 armas (inclui as 3 épicas) e 2 escudos. Cada peça tem a malha `Piece` com o pivô na articulação
  (Head, Body, ArmL/ArmR, LegL/LegR). O arco vai na mão esquerda.
- **Asas** em `Wings/` (Cinza, Crepúsculo, Brasalume): `Wings` → `WingL`/`WingR`, que batem.
- **Montarias** em `Mounts/` (Lagarto de Basalto, Lobo de Brasas): Body → Head, Tail, Leg0..3, com sela e brasas emissivas.
- **Mascotes** em `Pets/` (Diabrete de Cinza com asas, Lume Guardião com 3 orbes).

Regras no servidor (Core): slots `Wings`, `Mount` e `Pet`; asas dão dano/absorção/defesa e crescem com runas; montaria com `MOUNT`
(tecla R) e velocidade validada no servidor (desmonta ao atacar, cair ou tirar a montaria); Diabrete recolhe itens a 6 m e dá +10% de ouro;
Lume dá +15 de defesa e −5% de dano; 3 receitas de asas na Forja Arcana com a Pena de Brasa; 3 armas épicas (uma por classe);
absorção limitada a 80%; `GM ITEM <id> [nível]`. A aparência vai no `@S` (classe `Appearance`) e os outros jogadores veem tudo.

Cliente: `Gear.cs` pendura as peças no rig, liga o brilho de runa (propriedades `_Glow`/`_GlowColor` novas no shader, pulsando),
faíscas (ParticleSystem com o próprio material Kezor e cor emissiva; foi preciso ligar o módulo embutido
`com.unity.modules.particlesystem`), aura Ascendente, rastro (TrailRenderer) das armas épicas no golpe, asas batendo, montaria
(o cavaleiro senta na sela e quem anda é a montaria) e o mascote que segue por cima do ombro. O inventário ganhou os 3 slots.

Testes: 50/50 no Unity e no `dotnet test` (novo `GearTests.cs`: aparência no snapshot, asas, forja de asas, montaria, mascotes, épicas).

Capturas (Play pelo UnityMCP, modo local, com outros 2 jogadores no servidor embutido):
`Art/previews/equip_sem_item.png` (herói sem nada), `equip_equipado.png` (Rúnica + Fendente Rubro +15 + escudo +13 Ascendente
+ Asas de Brasalume +9), `equip_grupo.png` (os 3 heróis), `equip_asas.png` (os 3 tiers de asas de costas),
`equip_montaria.png` (Patrulheiro no Lobo de Brasas), `equip_pets.png` (Lume Guardião e Diabrete), `equip_combate_hud.png`
(golpe com HUD e os 10 slots no inventário).

## Marco 2: conjuntos, mapas, baú e contas (etapa 6, 22/11/2026)
Tudo original de Kezor. Scripts novos do Blender: `Art/scripts/kezor_world2.py` (Ilsa, baú, portal) e
`Art/scripts/kezor_world2_maps.py` (8 monstros e 6 peças de cenário); rodar depois de lib, humans, monsters, env e equip.
- **Conjuntos**: Placas Rúnicas e Véu agora têm 5 peças (itens 132, 133, 142, 143, 144; modelos Véu de calças/luvas novos).
  Bônus de conjunto completo (`ArmorSets`): Couro, Escamas, Véu e Placas Rúnicas.
- **Vários mapas** (`MapDefinition.CreateAll`): Vale de Brasalume (1), Pântano de Turfa Negra (2, nível 20+) e
  Cripta de Vidro Negro (3, nível 45+). Cada personagem, monstro e item tem `MapId`; snapshots, combate, grupo e aggro são por mapa.
  Portais (arco `PortalArco` com vórtice da cor do destino) e `WARP <código>` com custo e nível mínimo; morrer leva à vila.
  **A*** (`Pathfinder`, grade de 1 m) substituiu o desvio local: o herói contorna casas, poças e paredes de masmorra.
- **Pântano**: poças de turfa intransponíveis, árvores mortas, tocos, juncos e cogumelos fátuos; névoa verde.
  Monstros: Rastejante da Turfa, Bruxa do Junco, Carniçal Afogado, Mariposa Fátua (voa, asas batendo).
- **Cripta**: chão de ladrilhos só nos salões/corredores (`Floors`), paredes automáticas nas bordas, pilares, cristais e trono.
  Monstros: Sentinela de Vidro, Aranha de Obsidiana, Espectro Vítreo; chefe Soberano de Vidro Negro (drop garantido de arma épica ou asas).
- **Baú da cidade** (`Vault`, 8×15 + ouro até 2 bi): NPC Ilsa, a Guardiã, e baú de ferro na praça; comandos `VAULT/VPUT/VTAKE/VGOLDIN/VGOLDOUT/VCLOSE`,
  mensagem `@VAULT`; janela no HUD com arrastar entre baú e mochila. Itens `Bound` não entram.
- **Contas**: `REGISTER/AUTH/CHARS/PLAY`, até 3 personagens por conta, senha PBKDF2-SHA256 (120 000 iterações, sal de 16 bytes).
  O baú é da conta (compartilhado). Saves em JSON (`JsonFileRepository`, gravação atômica) no servidor dedicado: ao desconectar,
  a cada 60 s e ao desligar (Ctrl+C). O modo local continua como convidado. Tela de login/seleção de personagem no cliente.
- Testes: 71/71 no Unity e no `dotnet test` (novos: SetTests, MapTests, VaultTests, AccountTests).

Capturas: `Art/previews/bau.png`, `bau_ilsa.png`, `m2_portais.png`, `m2_pantano.png`, `m2_pantano_monstros.png`, `m2_cripta.png`,
`m2_cripta_trono.png`, `m2_morte_volta_vila.png`, `m2_login_personagens.png` (servidor dedicado real), `m2_online_jogando.png`
(depois de reiniciar o servidor: nível, ouro e posição voltaram do save).

## Marco 3: IA dos monstros, troca e guildas (etapa 7, 22/11/2026)
- **IA com A***: perseguição e volta ao ponto de origem usam o mesmo A* dos jogadores (`StepMonster`, recalcula quando o alvo anda
  1,5 m ou a cada 1 s). Contorna quinas da Cripta sem entrar na parede; leash de 16 m; se travar no caminho de volta, reaparece no spawn.
- **Troca segura** (`GameServer.Trade.cs`): `TRADE <nome>` (até 4 m), `ACCEPT/DECLINE`, `ADD/REMOVE <slot>`, `GOLD <n>`, `LOCK` (passo 1),
  `CONFIRM` (passo 2, só com os dois travados), `CANCEL`. Qualquer mudança — inclusive no item oferecido (usar parte da pilha, guardar no baú) —
  destrava os dois. Na confirmação o servidor revalida itens, ouro e espaço e troca tudo de uma vez; se algo falhar, nada muda.
  Afastar-se (mais de 6 m), trocar de mapa, cair ou sair cancela. Itens vinculados não são trocáveis. Mensagem `@TRADE`.
  Cliente: tecla **T** sobre outro jogador (ou `/trade Nome`), aviso com Aceitar/Recusar, janela ao lado da mochila.
- **Guildas** (`Guilds.cs`, `GameServer.Guild.cs`): fundar com **Hadrik, o Arauto das Guildas** (praça; nível 50, 500 mil de ouro),
  convidar (líder/oficial), aceitar, sair (a liderança passa ao oficial), expulsar (só cargo menor), promover/rebaixar (líder), dissolver.
  Chat de guilda `/g texto`. O nome da guilda vai no snapshot e aparece acima do herói para todos. Salvas em `guilds/<nome>.json`.
  Janela da guilda na tecla **G**. Modelo do Hadrik com estandarte (Blender, `kezor_world2.py`).
- Testes: 82/82 no Unity e no `dotnet test` (novos: MonsterAiTests, TradeTests, GuildTests).

Capturas: `Art/previews/m3_guilda.png` (3 heróis da guilda Brasaviva com o nome acima, janela de cargos e chat de guilda),
`m3_troca.png` (troca com itens dos dois lados, ouro e o parceiro já confirmado).

## Marco 4: loja pessoal, ranking e guerra de guildas (etapa 8, 22/11/2026)
- **Loja pessoal** (`GameServer.Shop.cs`): `SHOP ADD <slot> <preço>`, `SHOP OPEN <nome>` (só na vila; o herói fica parado — andar,
  atacar e pegar itens pedem para fechar), `SHOP REMOVE`, `SHOP CLOSE`, `SHOP VIEW <jogador>` (até 6 m) e `SHOP BUY <jogador> <slot> <preço>`.
  A compra confere item no mesmo espaço (`ReferenceEquals`), o preço anunciado (protege contra trocar o preço na última hora), ouro e
  mochila, e troca tudo de uma vez. A loja fecha se o dono sair da vila, cair, desconectar ou vender tudo. Mensagem `@PSHOP`.
  O nome da loja vai no snapshot e aparece numa placa acima do vendedor. Cliente: tecla **B** monta a barraca (duplo clique na mochila anuncia);
  clicar num vendedor abre a vitrine.
- **Ranking** (`Ranking.cs`): resumo de cada personagem (classe, nível, renascimentos, experiência) atualizado ao entrar, ao subir de nível e ao salvar;
  pontuação = 400 × renascimentos + nível. `RANKING GERAL|GUA|ARC|PAT|GUILDAS` manda `@RANK` (top 10) e responde a sua posição.
  Guildas: soma dos membros + 1000 por vitória em guerra. Salvo em `ranking.json` (reconstruído dos saves se faltar). Janela na tecla **K**.
- **Guerra de guildas** (`GameServer.War.cs`): `GUILD WAR <guilda>` (líder), `GUILD WARACCEPT/WARDECLINE` (líder do outro lado, 2 min),
  `GUILD WARINFO`. 10 minutos; cada abate entre as duas guildas fora da vila vale 1 ponto e não dá infâmia. No fim, o servidor anuncia
  o vencedor e grava vitória/derrota/empate na guilda (salvo). Faixa de placar e tempo no topo da tela; guilda inimiga em vermelho.
- Testes: 89/89 no Unity e no `dotnet test` (novos: ShopTests, RankingWarTests).

Capturas: `Art/previews/m4_loja.png` (placas "Bazar da Mirela" e "Escudos do Toran" e a vitrine), `m4_guerra.png` (placar Brasaviva 2 x 0 Corvos),
`m4_ranking.png` (geral) e `m4_ranking_guildas.png` (Brasaviva com 1 vitória).

## 4 classes novas originais (etapa 9, 22/11/2026)
- **Lâmina Arcana** (híbrida espada+magia), **Comandante do Estandarte** (lança, estandartes com aura de grupo, ordens em área),
  **Invocadora de Ecos** (até 2 ecos aliados com A*, maldições de lentidão e dano contínuo) e **Punho de Brasa** (manoplas,
  ataque a cada 0,5 s, Fluxo de Brasa). Todas originais do Kezor; números em `docs/DESIGN.md`.
- Cada classe: 6 skills, conjunto de 5 peças (Trama Arcana, Bastião, Véu dos Ecos, Couro Ígneo), 3 armas e 1 épica com rastro
  (Lâmina Astral Partida, Haste do Juramento Solar, Sino do Silêncio Eterno, Núcleo de Brasa Viva).
- Blender (`Art/scripts/kezor_classes2.py`): 4 corpos base (o Comandante leva o estandarte nas costas), 4 conjuntos × 8 peças,
  16 armas (manoplas com par _L para a mão esquerda) e os ecos Sombrio e Luminoso — 58 FBX.
- Cliente: criação em grade com as 7 classes (local e online), ranking com aba por classe, barra de 6 skills com ícones desenhados
  em código (`SkillIcons.cs`), estandarte com mastro, bandeira e anel de aura, nome verde nos ecos (fora da mira), efeitos de CAST,
  projétil da Invocadora e "FLUXO!" no Punho.
- Play pelo UnityMCP com as 4 novas (nível 120, épica +11 e conjunto +9) e as 3 antigas (nível 40): sem erros no console.
  Correções achadas no teste:
  - buff/estandarte/eco pedidos durante o ataque automático respondiam "aguarde" e se perdiam: agora entram numa fila (`PendingUtility`);
  - com a fila, a simulação passou a lançar os buffs de verdade: Invocadora subiu para 1,61× e Lâmina para 1,38×; reequilibrado
    (força dos ecos ENE/60 → ENE/110, Lâmina Ressonante +20% → +15%): Lâmina 165, Invocadora 146 contra média 125;
  - rótulos de estandartes no mesmo lugar se sobrepunham: escalonados por tipo;
  - ranking por classe dizia "você está em 0º lugar" para quem não é da classe.
- Ajustes finais: as peças e armas da Invocadora agora exigem energia como requisito principal; a força ficou baixa
  (0 a 10, ou seja, até 14 no +15), e a Invocadora com a força inicial (15) usa tudo. Fluxo de Brasa testado no Play contra o Arauto das Cinzas
  (`/gm event`): "FLUXO!" com golpe Combo de 417 (`classes_punho_fluxo.png`).
- Testes: 100/100 no Unity e no `dotnet test` (novos: ClassTests, com simulação de DPS, fila de skills e requisitos da Invocadora).

Capturas: `Art/previews/classes_criacao.png` (7 classes), `classes_lamina.png` (Lâmina Ressonante + Maré de Lâminas),
`classes_comandante.png` e `classes_comandante_estandartes.png` (estandartes com aura), `classes_invocadora.png` (dois ecos aliados),
`classes_punho.png` (manoplas épicas), `classes_ranking.png` (abas por classe) e `classes_antigas_patrulheiro.png`.

## Design e interface (etapa 10, 22/11/2026)
Estilo próprio do Kezor (fantasia sombria de brasa e cinza, cantos chanfrados); nada copia a interface de outro jogo.
- **Guia visual** em `docs/UI_GUIDE.md`: paleta (tokens USS), tipografia, espaçamentos, estados e componentes. Fontes **Cinzel**
  (títulos) e **Inter** (texto) em `Assets/Resources/Fonts`, licença SIL OFL 1.1 junto.
- **Arte desenhada em código** (`UiRaster` + `UiArtBaker`, menu Kezor ▸ Gerar arte da UI): molduras, faixa de título, botões em
  4 estados, slots, abas, campos, anel das orbes com gemas de brasa, vidro, moldura do minimapa, chapa da barra, toast, silhueta
  do herói e 14 glifos de menu. Tema em `Assets/Resources/UI/Kezor.uss`.
- **UI Toolkit** (`Assets/Scripts/Client/UI`): só apresentação; lê o `ClientState` e chama ações do `KezorGame`
  (`KezorGame.UiBridge.cs`). O IMGUI antigo foi removido. Escala pela altura (referência 1600×900) vezes a opção do jogador.
- **HUD**: quadro do herói, buffs com timer (o servidor agora manda buffs, auras e recargas no `@ME`), grupo, quadro do alvo,
  minimapa redondo com relevo, orbes com líquido animado, 6 skills com recarga radial e avisos, poções, exp segmentada, menu,
  chat com abas e toasts; nomes e números de dano em UI Toolkit.
- **Janelas** (14): personagem, inventário com silhueta e 10 slots, baú, troca, loja do NPC, forja, grupo, guilda, ranking,
  minha loja, vitrine, mapa e viagem, opções (escala, minimapa, volume, qualidade, FPS, VSync, tela cheia, teclas) e ajuda.
  Arrastam pelo título, guardam a posição, fecham no X ou Esc. Tooltip rico com conjunto e comparação; arrastar e soltar.
- **Telas de entrada**: login/conta, criação e seleção com o herói 3D girando num pedestal (RenderTexture, arrastar gira,
  opção de ver o equipamento da classe) e tela de carregamento entre mapas com dica.
- **Ícones** (`IconBaker`, menu Kezor ▸ Gerar ícones): 94 itens renderizados dos próprios FBX (armas na diagonal, contorno
  escuro) e poções/runas em 2D; 33 skills com emblema da classe e símbolo do tipo. Atlas 128 px + mapa de recortes.
- Testes: 101/101 no Unity e no `dotnet test` (novo: buffs e recargas chegam ao cliente). Play em 1080p e 1440p.
- `Build/ClientCheck` agora compila contra as DLLs do Unity 6 instalado.

Capturas (antes → depois): `ui_antes_hud.png` → `ui_depois_hud_1080p.png`, `ui_depois_hud_1440p.png`, `ui_depois_hud_pantano_1440p.png`;
`ui_antes_janelas.png` → `ui_depois_janelas.png`, `ui_depois_bau.png`; `ui_antes_criacao.png` → `ui_depois_criacao.png`,
`ui_depois_login.png`; carregamento: `ui_depois_carregamento.png`, `ui_depois_carregamento_pantano.png`.

## Heróis v2, inventário em grade e HUD em barra (etapa 11, 22/11/2026)
Convenções clássicas do gênero com arte própria do Kezor (sem copiar a interface de outro jogo).
- **Heróis v2 no Blender** (`Art/scripts/kezor_heroes2.py`): os 7 corpos base passaram de ~340 para ~4.000 triângulos,
  todas as arestas chanfradas, rosto completo (maxilar, queixo, nariz, sobrancelhas, olhos com íris, orelhas, boca), cabelo por
  classe, mãos com dedos e polegar, ombros, cotovelos, joelheiras, botas com dobra/biqueira/sola, cinto com fivela e bolsa,
  placas no peito; 3–4 tons por material. Pivôs e partes iguais: equipamento visível e animações continuam funcionando.
- **Equipamento v2**: os 8 conjuntos ganharam detalhes por estilo (ombreiras em camadas com friso e rebites, gorjal, emblema,
  placa das costas, escamas, crista, malar, protetor nasal, ponta de capuz, nós dos dedos, punhos, biqueiras, coxotes,
  joelheiras) e todas as peças e armas ganharam chanfro. **AO assado** (Cycles) nas cores de vértice; 106 FBX exportados.
- **Inventário em grade**: `Inventory` com casas e tamanho por item (`ItemSize`), mochila 8×8 e baú 8×15, encaixe, mover
  (`IMOVE`/`VMOVE`), casa de destino no `VPUT`/`VTAKE`, troca com simulação de espaço, equipar que desfaz se não couber,
  migração de saves (reempacota; sobra vai para o baú). Cliente: `GridView` com prévia verde/vermelha ao arrastar;
  ícones renderizados no formato de cada item (96 px por casa). 8 testes novos (`InventoryGridTests`).
- **HUD v3**: barra única na base com orbes nos berços das pontas, skills e poções no centro, menu nos dois lados e
  experiência segmentada embaixo (arte nova `hudbar` e `cradle`).
- Testes: 109/109 no Unity e no `dotnet test`. Play em 1080p.

Renders/capturas: `herois_v2_antes.png`, `herois_v2_depois.png`, `herois_v2_equipados.png`, `hud_v3_hud.png`,
`hud_v3_inventario.png`, `hud_v3_bau.png`, `hud_v3_arrastar.png`.

## Estilo heroico (etapa 12, 22/11/2026)
Direção em `docs/ESTILO_REFERENCIA.md` (princípios gerais do gênero, motivos próprios: brasa, cinza, runas; nenhuma
referência a arquivos de outros jogos).
- **Conjuntos v3** (`Art/scripts/kezor_style3.py`): ombreiras grandes em 3–4 placas com aro, filigrana e rebite de runa,
  chama de brasa erguida nos pesados, gorjal alto, peitoral com filigrana e emblema, **capa em três painéis com brasão de
  brasa**, tabardo, elmos marcantes por estilo, punhos em sino, biqueiras de metal; contraste metal polido × couro/tecido
  fosco na cor de vértice. 64 peças reexportadas.
- **Interface em metal trabalhado**: molduras com cantos de filigrana, títulos centralizados em Cinzel com brasão,
  brasão central na barra, orbes de 172 px com anel de 24 facetas, cravos e coroa de chama.
- **Efeitos**: refino +11/+13/+15 bem mais forte, aura no chão (1 ou 2 anéis girando + faíscas), épicas com dois sistemas
  de faíscas e rastro mais largo, asas 30% maiores.
- Testes: 109/109. Capturas: `estilo_antes_*.png` → `estilo_depois_herois.png`, `estilo_depois_herois_costas.png`,
  `estilo_depois_hud.png`, `estilo_depois_janelas.png`.

## Seleção em fileira (etapa 13, 22/11/2026)
- **Cena noturna original** `Environment/SelecaoPatio` (`Art/scripts/kezor_select.py`, ~12k tris): pátio de lajes,
  muralha de basalto com pontas de ferro e torres, quatro braseiros com fogo animado e faíscas, árvores mortas, degraus.
- Até **5 heróis por conta** (era 3) lado a lado, com modelo real, equipamento, asas, brilho de refino e animação parada;
  cada um ocupa ~25% da altura da tela.
- **Placa** sobre cada herói: nome, guilda e cargo, classe, nível, renascimentos; "Vazio" nos lugares livres (clique abre
  a criação). Clique seleciona (anel de brasa girando no chão, leve zoom); duplo clique ou Enter entra; setas trocam.
- **Criar** e **Menu** (opções, sair da conta, fechar o jogo) à esquerda; **Apagar** e **Entrar** à direita. Apagar pede o
  nome digitado e o servidor confere (`DELCHAR nome nome`; não apaga dentro do mundo nem líder de guilda).
- `CHARS` agora traz `nome:classe:nível:renascimentos:guilda:cargo:aparência`; igual nos modos local, LAN e servidor.
- Testes: 112/112 (3 novos em `CharacterSelectTests`). Capturas: `selecao_fileira.png`, `selecao_apagar.png`,
  `selecao_vazio.png`, `selecao_criar.png`.

## Escala e brilho (etapa 14, 22/11/2026)
- **Vila 1,5× maior** em volta do herói: muralha (raio 24, 5,6 m de altura), torres, casas, fonte, postes de lanterna novos
  (4 m, com braço e lanterna), baú, portais (arco 1,8×) e as bancas da Tália e do Bram. Árvores 1,75× (5–12 m), pedras,
  tocos e pilares da cripta também cresceram. Colisão, posições de NPCs, área segura, portais e grupos de monstros perto
  da vila acompanham (`MapDefinition.TownScale`); alcance do baú e da guilda 6,5 m; AOI 60 m.
- **Câmera alta**: 50°, distância padrão 32 m, zoom entre 20 e 44 m.
- **Noite com braseiros**: luar azul; 8 braseiros nos portões (com colisão), postes, forja, fonte e portais iluminam o
  entorno com luzes locais no shader (as 8 mais próximas, com tremulação de chama).
- **HDR + bloom**: refino +11/+13/+15, armas épicas, asas de Brasalume e faíscas passam de 1 no buffer HDR; bloom próprio
  (pré-filtro com joelho, 6 níveis, ombro suave). Opções: liga/desliga e intensidade 0–200%.
- `GM TP x z` para capturas. Teste novo `TownScaleKeepsNpcsChestPortalsAndBraziersCoherent`; 113/113.
- Capturas: `escala_antes.png` → `escala_depois.png`, `escala_portao.png`, `escala_brilho_depois.png`,
  `escala_brilho_sem_bloom.png`.
- A HUD mais escura e discreta, com botões pequenos nos cantos, entra junto da HUD v4 (etapa seguinte), que refaz a barra.

## Inventário v4 (etapa 15, 22/11/2026)
- **Painel alto preso à direita**. No bloco de equipamento cada slot tem o formato do que recebe: arma e escudo 2×3,
  armadura 2×3, asas 3×2, elmo, luvas, calças e botas 2×2, joias, mascote e montaria 1×1. A disposição é mascote/montaria,
  amuleto, elmo e asas no alto; arma, anel, armadura, anel e escudo no meio; luvas, calças e botas embaixo. Slot vazio mostra
  uma silhueta-guia (12 desenhos novos em `UiArtBaker`).
- Mochila 8×8 com números de pilha grandes, fundo na cor da raridade (raro azul, refinado dourado, épico laranja,
  Ascendente verde) e contador de casas livres. Rodapé com ouro e os botões Reparar, Forja, Compactar/Expandir e Fechar.
- **Joias novas** no servidor: slots Amuleto, Anel 1 e Anel 2; 4 anéis e 2 amuletos (vida, mana, resistência, dano), com
  drop, loja, salvamento, protocolo e ícones 2D próprios. Tooltip mostra os atributos.
- Testes: `JewelryTests` (3); 116/116. Captura: `inventario_v4.png`.

## HUD v4 (etapa 16, 22/11/2026)
- As orbes saíram. No lugar entraram os **Cristais de Vida e Mana**: gema hexagonal com engaste de bronze, líquido com
  superfície ondulando, facetas e borda de brasa pulsando com pouca vida.
- **Vigor** e **Escudo** são mecânicas novas no servidor (ver `docs/DESIGN.md`), com barras verticais finas ao lado dos cristais.
- **Atalhos Q W E R** de consumível: arrastar da mochila, total na mochila, validação no servidor, salvos por personagem.
- Skills 1–5 e o slot de **magia ativa** (botão direito), menu de 8 botões quadrados pequenos, experiência em 20
  segmentos com dica. Base escura e discreta (fundo quase preto com filete de bronze).
- O mapeamento do teclado do Input System passou a cobrir todas as letras e números. Antes G, K, B, O, T e as skills 5 e 6
  não respondiam.
- Testes: `VitalsTests` (4). O simulador de DPS passou a respeitar o vigor; 120/120. Captura: `hud_v4.png`.

## Árvore da Ascensão (etapa 17, 22/11/2026)
- Ascensão permanente liberada no nível 400, com curva própria, 1 ponto por nível e máximo 200 (regras no `docs/DESIGN.md`).
- 3 ramos por classe, com 15 nós cada: Bastião, Fúria e o ramo exclusivo da classe. São 7 ramos de classe com nomes próprios.
  Os nós têm ranks, pré-requisitos e linhas liberadas por pontos.
- Protocolo LEARN/RESET (redistribuição custa ouro), `@ASC` para o cliente e salvamento no personagem.
- Janela de tela cheia na tecla A, com 13 emblemas novos (um por efeito) tingidos pela cor do ramo. Aprendido fica dourado,
  disponível fica em brasa e trancado fica apagado.
- Testes: `AscensionTests` (4); 124/124. Captura: `ascensao.png`.

## Login com lista de servidores (etapa 18, 22/11/2026)
- Tela nova: fundo 3D cinematográfico, logotipo e painel pequeno com Local, LAN e os servidores do `servers.json`.
  Mostra canais, status por ping TCP (comando STATUS), população e latência. Fluxo servidor → conta → seleção, lembrando
  o último servidor e a conta (sem a senha). Menu com Opções, Créditos e Sair. Nome, canal e limite do dedicado são configuráveis.
- Correção: as chamas dos braseiros da seleção perdiam o pivô na importação (apareciam todas no centro). Também ajustei o
  bloom e a luz da seleção, para os heróis +15 não estourarem.
- Testes: `ServerListTests` (4: parser, STATUS, servidor lotado, ping TCP real); 128/128.
- Capturas: `login_servidores.png` (com o servidor dedicado de verdade rodando), `login_conta.png`,
  `login_selecao_servidor.png`, `login_creditos.png` e `selecao_fileira.png` (refeita).

## Seleção v2 (etapa 19, 22/11/2026)
- Seguindo o formato de referência do dono (convenção do gênero, arte e cena próprias do Kezor):
  - plano aberto e escuro, heróis menores na faixa de baixo, mais espaçados (passo 2,6 m);
  - cena nova `SelecaoPatio` v2: portão rúnico de basalto com grade, braseiros em pilares, escadarias laterais e mais árvores mortas;
  - placas compactas logo acima da cabeça: nome, (guilda · cargo), classe e nível, renascimentos;
  - botões pequenos nos cantos: Criar e Menu à esquerda, Apagar e Entrar à direita.
- Captura: `selecao_v2.png`.
- Ajuste seguinte: faixa escura fina na base com os botões pequenos, heróis a 86% (~17% da altura), câmera um pouco mais longe e bloom mais forte no palco escuro (refino alto, épicas e asas). Captura: `selecao_v3.png`.

## Inventário v5 (etapa 20, 22/11/2026)
- Seguindo o formato de referência do dono (convenção do gênero, com arte própria):
  - painel "Equipamento" alto, colado na borda direita e no topo;
  - slots: mascote, amuleto, elmo e asas em cima; arma, montaria, armadura e escudo no meio; luvas, anel, calças, anel e botas embaixo;
  - slot ocupado em carvão com borda na cor da raridade (paleta do Kezor); vazio cinza com silhueta-guia;
  - mochila 8×8 com fundo dos itens em carvão quente e números de pilha grandes no canto de cima;
  - caixa de ouro com valor grande; botões quadrados: X para fechar à esquerda; reparar, forja e compactar/expandir (brasa) à direita. Só a estrutura segue a referência; cores e acabamento são do Kezor.
- Captura: `inventario_v5.png`.

## HUD v5 (etapa 21, 22/11/2026)
- A estrutura segue a referência do dono e a paleta é do Kezor.
- Barra única e compacta, nesta ordem: atalhos Q W E R de poção (com o total), Cristal de Vida com o número, barra vertical
  de Vigor com o número embaixo, skills 1–5, magia ativa (moldura em âmbar, botão direito), barra vertical de Escudo com o
  número embaixo, Cristal de Mana e uma fileira de 5 botões quadrados (Inventário, Personagem, Guilda, Ascensão, Opções).
- Experiência em segmentos finos embaixo da barra, com a porcentagem no fim e o detalhe na dica.
- Ranking (K), mapa (M), grupo (P) e ajuda (H) seguem nas teclas.
- Captura: `hud_v5.png`.

## Árvore da Ascensão v2 (etapa 22, 22/11/2026)
- A estrutura segue a referência do dono e a paleta é do Kezor.
- Cabeçalho em caixas: classe, nível de Ascensão, pontos de Ascensão, experiência em % e Redistribuir.
- Três colunas com o título do ramo e os pontos gastos ("Bastião: 12"). Cada nó é uma carta de emblema com a caixinha do
  rank ao lado (âmbar no máximo) e uma seta de brasa até o nó de baixo quando aprendido.
- A janela fica acima da HUD, que continua visível.
- Captura: `ascensao_v2.png`.

## Itens por clique e câmera v2 (etapa 23, 22/11/2026)
- **Clique para pegar, clique para soltar**. O 1º clique esquerdo prende o item ao cursor, no tamanho da grade e com prévia
  verde/vermelha. O 2º clique solta na casa ou slot sob o cursor (mochila, equipamento, baú, troca, loja ou atalhos Q W E R).
  - Em cima de um item do mesmo tamanho, os dois trocam e o antigo vai para o cursor (`IMOVE`/`VMOVE` respondem `OK troca n`).
  - Clique no chão do mundo larga o item (`DROP`), pedindo confirmação se ele for valioso. Esc ou clique direito devolve.
  - Duplo clique e clique direito ainda equipam ou usam. O arrastar antigo saiu.
  - Com item no cursor, cliques não andam nem atacam. O servidor valida cada movimento; se recusar, o item continua na mão.
- **Câmera**: nada reseta mais rotação e zoom. A causa era a vista do herói refeita (troca de item, mapa) zerar distância e
  ângulo, e o passeio do login mexer no mesmo ângulo. Rotação e zoom ficam salvos (`kz.camYaw`, `kz.camDist`) e valem entre
  mapas e ao relogar. Só a tecla **Home** volta ao padrão.
- **Zoom**: padrão a 30 m e 50°, com o herói em ~10% da altura em 1080p. Limites de 16 a 36 m e rolagem suave.
- Testes: `ItemHandTests` (5); 133/133. Capturas: `camera_v2.png`, `inventario_clique.png`.
- **Correção: o herói não se mexe ao trocar equipamento.**
  - Causa: ao mudar o visual, a vista do herói era destruída e recriada na posição do último snapshot, olhando para +Z e com
    a animação zerada. Por isso ele virava, dava um pulinho e a câmera "encaixava" de novo.
  - Agora a vista nova herda a posição já interpolada, a direção, a passada e a fase da antiga, e a câmera não encaixa.
  - Cliques na interface também não viram comando de mundo, nem no quadro seguinte.
  - O servidor não mexe em posição nem movimento ao equipar (teste novo `EquippingDoesNotMoveStopOrTurnTheHero`); 134/134.

## Cidade v2 e polimento dos props (etapa 24, 22/11/2026)
- **31 modelos novos** no Blender (`Art/scripts/kezor_town2.py`), low poly sem cara de caixa e com AO assada:
  - prédios: casas Casa2A, Casa2B e Casa2C e o Sobrado2, com telhas em fileiras, beirais, pontas de caibro, enxaimel,
    janelas com venezianas, portas de tábuas com ferragens, chaminés de pedra, lanterna na porta e floreira;
  - defesa: muralha em blocos irregulares com ameias chanfradas, torre redonda em fiadas com telhado cônico e bandeira,
    arco de portão com aduelas, chave de runa e grade erguida;
  - praça e distritos: fonte de 12 lados com degrau e gema, templo com colunata, forja com fornalha, estábulo com baias e
    feno, três barracas de feira com toldo curvo;
  - natureza: duas árvores com galhos e copa em grupos, dois pinheiros irregulares e duas pedras facetadas;
  - objetos: barril, caixote, carroça, poço, banco, vaso, cerca, estandarte, poste com lanterna, braseiro em tripé e baú.
- **Vale de 260 m e vila de raio 45 m** (antes 24): praça, rua circular, quatro distritos, 24 casas, postes nas ruas,
  braseiros nos portões, 13 NPCs (4 guardas e 5 moradores novos com fala própria). A planta é original do Kezor.
- Colisão, zona segura, renascer, portais e grupos de monstros foram refeitos para o mapa maior. A vila é desenhada em 8 fatias
  (culling por setor); ~140 fps no editor em 1080p.
- Os testes com coordenadas fixas passaram a usar o centro do Vale. Teste novo `CityV2IsBigNavigableAndCoherent`
  (caminho de cada portão até a praça e da praça até cada NPC); 134/134.
- Capturas: `cidade_v2_visao.png`, `cidade_v2_praca.png`, `cidade_v2_mercado.png`, `cidade_v2_portao.png`,
  `props_v2_casas.png`, `props_v2_sobrado.png`, `props_v2_pecas.png`. Antes: `escala_depois.png`.

## Etapa 25 — mid poly estilizado (heróis, cidade, ambiente)
- Shader `Kezor/LowPoly` refeito com `KezorCommon.cginc`: textura de detalhe por tipo de material (id na 2ª UV, projeção
  triplanar no espaço do objeto, relevo de normal), névoa rasteira, instancing e vento (`_Wind`). Novo `Kezor/Terrain`
  (splat por vértice: grama, terra, calçamento, cascalho). Texturas 100% procedurais (`DetailTextures.cs`, 14 camadas).
- Blender `Art/scripts/kezor_midpoly.py`: caixas e membros com cantos arredondados (substitui `cbox`, `B.box`, `B.seg`),
  normais suaves ponderadas, AO por canto, id de material na UV2, telhas curvas, hera e musgo (`Muralha2Hera`).
  Refeitos e exportados: 7 classes, 8 conjuntos, armas, escudos, asas (109), 32 peças da cidade e natureza, peças do
  Pântano/Cripta, portal, pedra e árvore morta.
- LODs automáticos (Mesh LOD do Unity 6) em heróis, equipamentos e asas. ~25k tris por herói equipado no LOD0.
- Terreno suave (célula 1,25 m) com splat; grama, flores (inclusive a flor-brasa), arbustos, folhas, pedrinhas e poças
  por GPU instancing (`GroundScatter`, ~56 mil instâncias no Vale, desenhadas só perto da câmera); vagalumes, poeira e
  brasas dos braseiros/forja (`AmbientFx`). Cripta com lajes chanfradas.
- Desempenho no editor, 1080p: Vale ~125-130 fps, Pântano ~140, Cripta ~143. Testes 136/136 (Unity e dotnet).
- Capturas: `midpoly_herois_antes(_perto).png` × `midpoly_herois_depois(_perto).png`, `midpoly_cidade_antes.png` ×
  `midpoly_cidade_depois.png`, `midpoly_campo_depois.png`, `midpoly_pantano_depois.png`, `midpoly_cripta_depois.png`.
- Falta: redesenho das silhuetas peça por peça (elmos curvos, ombreiras em camadas, capas com dobras, rosto e mãos).

## Cidade v4 — Brasalume remodelada (etapa 26, 27/11/2026)
- A vila antiga (circular, modelos v2) foi removida: 28 FBX da cidade v2 apagados e `kezor_town3.py` aposentado.
- **Kit novo no Blender** (`Art/scripts/kezor_city4.py`, 28 modelos com AO assada e id de material na UV2): muralha de
  cantaria com ameias, fiadas e hera; torre redonda com telhado cônico de ardósia; portão fortificado com duas torres,
  grade, brasão e estandartes; quatro casas (pedra, enxaimel, sobrado de 3 andares, casa com telheiro); Taverna,
  Loja, Ferraria com forja, Capela com campanário e rosácea, Salão das Guildas com pórtico e torreões, Estábulo;
  Fonte da Brasa com gema, piso de lajes da praça; barracas (vermelha, azul, amarela), poste com lanterna, braseiro,
  estandarte, poço, banco, canteiro, caixotes, barris e obelisco. Casas com ~25 mil triângulos.
- **Planta nova**: cidade murada quadrada de 104 m de lado, portões nos quatro lados, 12 torres, praça circular calçada
  com a fonte, avenidas em cruz, anel interno, rua junto à muralha, quarteirões fechados de casas, mercado (NE),
  capela (NO), salão das guildas (SE), taverna (SO), ferraria e estábulo. 51 prédios, 80 peças, 13 NPCs.
- Colisão por retângulos girados (`ObstacleBox`) para casas e torres de portão, zona segura quadrada, terreno/minimapa/
  vegetação seguindo as ruas novas (`IsTownStreet`), som da forja na Ferraria. Caminhada na vila mantida em 2,2 m/s.
- Testes: `CityV4IsWalledNavigableAndCoherent` e `WallBlocksAndGatesRoute` refeitos; 157 ok (dotnet).
- Capturas (Blender, câmera do jogo): `cidade_v4_visao.png`, `cidade_v4_praca.png`, `cidade_v4_mercado.png`,
  `cidade_v4_portao.png`, `cidade_v4_taverna.png`, `cidade_v4_capela.png` e as versões `_noite`.

## Pendências
- Teclas ainda não são reconfiguráveis (a janela de opções só lista); rebinding fica para depois.
- Duas skills básicas da mesma classe às vezes têm emblemas parecidos (ex.: Jab e Gancho); dá para diferenciar mais.
- Sem TLS na conexão: a senha vai pela rede até o servidor (que só guarda o hash). Usar TLS/túnel antes de abrir ao público.
- O recorte da câmera às vezes pega a borda de uma casa ao lado do herói (ela fica pontilhada).
- Peças de Rúnica para calças/luvas/botas existem como modelo, mas ainda não há itens com esses ids.
- O cavaleiro montado não tem animação própria de ataque (desmonta ao atacar, como manda a regra).
- As faíscas são quadradinhos (sem textura), no estilo low poly; dá para trocar por uma malha de losango se quiser.
