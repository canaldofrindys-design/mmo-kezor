# Kezor — Documento de design (vertical slice)

**Premissa.** O Véu entre os mundos rachou e as Cinzas avançam sobre os vales. Brasalume é uma vila murada que mantém acesa
a Fonte de Brasas, a última chama que repele as Cinzas. Heróis caçam no Bosque Cinzento ao redor para ficar fortes antes
que a próxima Invasão chegue ao portão sul.

## Classes
| Classe | Código | FOR/AGI/VIT/ENE | HP base +nv +VIT | MP base +nv +ENE | Dano | Alcance básico |
|---|---|---|---|---|---|---|
| Guardião Rúnico | GUA | 28/20/25/10 | 110 +2 +3 | 20 +0,5 +1 | FOR/6 ~ FOR/4 | 2,3 (corpo a corpo) |
| Arcanista do Véu | ARC | 18/18/15/30 | 60 +1 +2 | 60 +2 +2 | magia ENE/9 ~ ENE/4 | 7 |
| Patrulheiro Selvagem | PAT | 22/25/20/15 | 80 +1 +2 | 30 +1,5 +1,5 | FOR/14+AGI/7 ~ FOR/8+AGI/4 | 8 |
| Lâmina Arcana | LAM | 24/20/20/22 | 85 +1,5 +2,5 | 40 +1,5 +1,5 | FOR/7 ~ FOR/5 **+** magia ENE/15 ~ ENE/9 (skills híbridas somam os dois) | 2,4 |
| Comandante do Estandarte | COM | 24/18/26/20 | 100 +1,8 +2,8 | 35 +1,2 +1,5 | FOR/7 ~ FOR/5 | 2,9 (lança) |
| Invocadora de Ecos | INV | 15/18/18/32 | 60 +1 +2 | 65 +2 +2 | magia ENE/10 ~ ENE/5 | 6,5 |
| Punho de Brasa | PUN | 26/27/22/10 | 95 +1,6 +2,6 | 25 +0,8 +1 | FOR/12+AGI/18 ~ FOR/9+AGI/12 | 1,9 · ataque a cada 0,5 s (os outros 0,6 s) · anda 5,8 |

As quatro classes novas são **originais do Kezor** (arquétipos genéricos do gênero: híbrido, suporte de auras, invocador e lutador
desarmado). Nenhuma copia nome, kit, visual ou regra de desbloqueio de outro jogo; todas podem ser criadas desde o início.

5 pontos por nível · nível máx. 400 · exp para o próximo = 5·N²·(N+10)+100.
**Renascimento**: no nível 400, custa 1.000.000 × (renascimentos+1) de ouro; volta ao nível 1 com atributos base e 500 pontos por renascimento (máx. 50).
Taxa de ataque = 5·Nível + 1,5·AGI + FOR/4 · Taxa de defesa = AGI/3 · Acerto = TA/(TA+TD), entre 5% e 95%.

### Skills
| Skill | Classe | MP | Efeito |
|---|---|---|---|
| Corte Rúnico | Guardião | 8 | 130% +15 |
| Ciclone de Aço (nv 20) | Guardião | 18 | 110% +30, área raio 3 |
| Dardo Etéreo | Arcanista | 3 | magia +8 |
| Chuva de Brasas (nv 15, ENE 50) | Arcanista | 16 | magia 110% +30, área 3,5 |
| Disparo Perfurante | Patrulheiro | 5 | 115% +10 |
| Saraivada (nv 15) | Patrulheiro | 14 | 100% +20, área 3 |

| **Lâmina Arcana** | | | |
| Fio Encantado | LAM | 6 | híbrida 115% +12 |
| Onda Arcana | LAM | 12 | híbrida 95% +20, área 2,5 |
| Lâmina Ressonante (nv 10) | LAM | 20 | buff: +15% de dano por 15 s (recarga 20 s) |
| Corte Estelar (nv 20) | LAM | 18 | híbrida 150% +25 |
| Maré de Lâminas (nv 40) | LAM | 28 | híbrida 125% +40, área 3,5 |
| Eclipse da Lâmina (nv 80) | LAM | 40 | híbrida 185% +50, lentidão 30% por 3 s |
| **Comandante do Estandarte** | | | |
| Estocada de Lança | COM | 5 | 125% +12, alcance 3,1 |
| Estandarte da Fúria | COM | 25 | estandarte 20 s, raio 6: grupo +15% de dano (+1% a cada 50 ENE) |
| Ordem: Investida! (nv 10) | COM | 14 | 115% +20, área 3 |
| Estandarte da Muralha (nv 20) | COM | 25 | estandarte: grupo +20 de defesa (+1 a cada 50 ENE) e taxa de defesa |
| Estandarte do Vento (nv 40) | COM | 25 | estandarte: grupo anda +20% |
| Ordem: Cerco de Ferro (nv 80) | COM | 35 | 160% +60, área 4, lentidão 25% por 3 s |
| **Invocadora de Ecos** | | | |
| Sussurro do Eco | INV | 4 | magia 100% +8 |
| Eco Sombrio | INV | 30 | invoca um eco por 30 s (até 2; o mais antigo some) |
| Maldição do Peso (nv 10) | INV | 10 | magia 80% +10, lentidão 40% por 5 s |
| Chaga Ecoante (nv 20) | INV | 14 | magia 60% +10, mais 30% do golpe por segundo durante 6 s |
| Eco Luminoso (nv 40) | INV | 40 | invoca um eco guardião mais resistente por 30 s |
| Coro dos Ecos (nv 80) | INV | 38 | magia 140% +50, área 4, chaga 20% por 4 s |
| **Punho de Brasa** | | | |
| Jab de Brasa | PUN | 3 | 105% +6 |
| Gancho Ígneo | PUN | 7 | 130% +14 |
| Passo da Chama (nv 10) | PUN | 15 | buff: +25% de velocidade e +15% de dano por 10 s |
| Rajada de Punhos (nv 20) | PUN | 14 | 3 golpes de 50% +4 (cada um conta para o Fluxo) |
| Coluna de Cinzas (nv 40) | PUN | 22 | 135% +40, área 2,5 |
| Fúria do Núcleo (nv 80) | PUN | 34 | 190% +50 |

**Estandartes** (Comandante): objetos no chão (`B` no snapshot), um de cada tipo por Comandante; a aura vale para o dono e o grupo
dentro do raio de 6 m (não soma dois estandartes do mesmo tipo: vale o maior).
**Ecos** (Invocadora): monstros aliados controlados pelo servidor (A*), força = 1 + ENE/110 + nível/100; atacam o alvo da dona,
não podem ser alvo, e somem ao acabar o tempo, se a dona sair, morrer ou trocar de mapa. O abate conta para a dona.
**Skills sem alvo** (buff, estandarte, eco) pedidas no meio do golpe automático entram numa fila e saem no próximo intervalo, sem parar o ataque.
**Maldições**: lentidão reduz a velocidade do monstro; chaga dá dano por segundo creditado a quem lançou.
**Fluxo de Brasa** (Punho, nv 10+): 5 golpes no mesmo alvo com no máximo 1,5 s entre eles → explosão extra de (FOR+AGI)×0,8 (acerto Combo).

**Equilíbrio** (teste `NewClassesAreBalancedAgainstTheOriginalThree`): 60 s contra um alvo de nível 100, nível 100, atributos de
cada classe e arma de nível médio. DPS: Guardião 110, Arcanista 129, Patrulheiro 135 (média 125); Lâmina 165, Comandante 138
(sem contar a aura no grupo), Invocadora 146 (com ecos e chaga), Punho 166. Todas ficam entre 0,7× e 1,4× da média das três originais.

**Combo do Guardião** (nv 10+): 3 skills seguidas, com pelo menos 2 diferentes, em até 3 s → golpe extra de (FOR+AGI)×1,5.

### Tipos de acerto
Normal (aleatório mín–máx) · **Crítico** (dano máximo; +5% por item com Sorte) · **Golpe Ascendente** (máx +20%; itens Ascendentes) · **Combo**.
Dano = base × %skill + fixo − defesa − redução%; piso = max(1, nível/10).

## Itens
- Bônus por nível de refino: +3 por nível até +9; depois +4, +5 … +9 por nível (**+15 = +66**).
- Opção adicional (Runa Vital): +4 por ponto, máx. +28. **Ascendente**: +25% do status base + uma opção especial
  (+10% Golpe Ascendente, dano + nível/20, HP ao abater, +10% taxa de defesa, −4% dano recebido, +4% HP).
- Requisito de FOR/AGI cresce 3% por nível. Durabilidade < 20% = status pela metade; 0 = item não conta.
- Conjuntos: Couro (todas), Escamas (Guardião/Patrulheiro), Véu (Arcanista), Placas Rúnicas (Guardião).

### Runas
| Runa | Uso | Sucesso | Falha |
|---|---|---|---|
| Runa da Aurora | +0→+6 | 100% | — |
| Runa do Eclipse | +6→+9 | 50% (75% com Sorte) | +6 cai para +5; +7/+8 voltam a +0 |
| Runa Vital | opção +4 (até +28) | 50% | opção zerada |
| Cristal do Vórtice | catalisador da Forja | — | — |

### Forja Arcana (Ferreiro Bram, só na vila)
| Alvo | Base | com Sorte | Aurora+Eclipse | Vórtice | Ouro |
|---|---|---|---|---|---|
| +10 | 60% | 75% | 1+1 | 1 | 2 kk |
| +11 | 55% | 75% | 2+2 | 1 | 4 kk |
| +12 | 50% | 70% | 3+3 | 1 | 6 kk |
| +13 | 45% | 65% | 4+4 | 1 | 8 kk |
| +14 | 40% | 60% | 5+5 | 1 | 10 kk |
| +15 | 35% | 55% | 6+6 | 1 | 12 kk |
Falha destrói o item; runas sempre consumidas. Chance de levar +9 a +15 sem Sorte ≈ 1%.
**Arma do Vórtice**: equipamentos +4~+8 + 1 Cristal do Vórtice (+8% por Aurora, +5% por Eclipse); taxa = Σ(nível·4 + opção·3 + Sorte 5), máx. 80% → Lâmina do Vórtice.

**Asas** (material: Pena de Brasa, cai de monstros nível 14+ a 4%; o Arauto deixa 3):
| Asas | Receita | Taxa | Ouro | Se falhar |
|---|---|---|---|---|
| Asas de Cinza (nível 40) | arma/armadura +7 ou mais + 5 Penas + 1 Aurora | 50% + 5%/nível acima de +7 (+10% Sorte), máx. 75% | 1 kk | perde tudo |
| Asas do Crepúsculo (nível 80) | Asas de Cinza + 10 Penas + 1 Eclipse + 1 Vórtice | 40% + 3%/nível das asas, máx. 70% | 5 kk | as asas voltam |
| Asas de Brasalume (nível 150) | Asas do Crepúsculo +7 + 20 Penas + 2 Vórtice + 1 Aurora + 1 Eclipse | 30% + 4%/nível acima de +7, máx. 60% | 20 kk | as asas voltam |

## Equipamento visível, asas, montarias e mascotes
- O herói aparece com o que veste: arma, escudo, elmo, armadura, calças, luvas, botas, asas, montaria e mascote.
  O servidor manda a aparência no snapshot (`@S`, campo `id.nível.asc,...,/montado`), então todos veem o mesmo.
- **Brilho de runa**: +7 brilho azulado, +9 dourado com faíscas, +11 alaranjado, +13 violeta, +15 ciano intenso
  (a arma brilha mais; armaduras e asas brilham pela metade; armadura +11 ou mais solta faíscas pelo corpo).
  Qualquer peça **Ascendente** acende uma aura verde nos pés.
- **Asas** (slot próprio, aceitam runas): dano +12/+20/+32% e absorção 10/18/26% (+1% de dano e +0,5% de absorção por nível de runa)
  e defesa 8/16/28. Absorção total limitada a 80%.
- **Montarias** (loja da Tália; **R** monta/desmonta): Lagarto de Basalto (nível 20, +35% de velocidade, 1,5 kk) e
  Lobo de Brasas (nível 50, +60%, 6 kk). O servidor move o herói, então a velocidade não pode ser forjada pelo cliente.
  Atacar, usar skill, cair ou tirar a montaria desmonta.
- **Mascotes** (loja da Tália): Diabrete de Cinza (nível 10, 300 k) recolhe os itens do dono num raio de 6 m e dá +10% de ouro;
  Lume Guardião (nível 25, 800 k) dá +15 de defesa e −5% de dano recebido.
- **Joias** (slots Amuleto, Anel 1 e Anel 2; só atributos, 1×1, não se desgastam nem aceitam runas): Anel de Basalto (-4% de dano
  recebido), Anel do Pulso Vivo (+60 de vida), Anel da Maré Arcana (+50 de mana), Anel da Brasa Afiada (+5% de dano, só cai),
  Amuleto do Lume (+120 de vida, -3% de dano recebido), Amuleto da Cinza Rúnica (+80 de mana, +6% de dano, só cai).
  O anel entra no primeiro dedo livre; com os dois ocupados troca o Anel 1. Caem com 1,2% de chance (chefe 25%) até o nível do
  monstro + 5; os comuns e o Amuleto do Lume estão na loja da Tália.
- **Armas épicas** (caem do Golem de Basalto e do Arauto): Fendente Rubro (Guardião), Cetro da Aurora Velada (Arcanista)
  e Arco Sussurro de Brasa (Patrulheiro), com faíscas próprias e rastro no golpe.

## Mapa: Vale de Brasalume (160 × 160)
- **Vila de Brasalume v2** (centro do Vale de 260 m; área segura de raio 44): muralha de raio 45 com quatro portões em arco
  (vão de ~8 m) entre torres redondas.
  - Praça da Fonte de Brasas, onde o herói renasce, com raio de 11 m. Uma rua circular a 20 m e quatro ruas retas até os portões.
  - Distritos:
    - mercado a nordeste (Mercadora Tália, quatro barracas);
    - forja a sudeste (Ferreiro Bram);
    - templo a noroeste (Irmã Vesna);
    - estábulo a sudoeste (Cavalariço Doran);
    - Hadrik (guildas) e Ilsa com o baú, na beira da praça.
  - 24 casas em dois anéis, em quatro modelos: Casa2A, Casa2B, Casa2C e Sobrado2.
  - Postes de lanterna ao longo das ruas e braseiros nos portões. Guardas nos quatro portões e moradores com falas próprias.
  - Monstros, portais (a leste) e o bosque ficam fora da muralha.
- **Escala do cenário**: o herói tem ~1,8 m; casas 7–9 m, torres ~11 m, muralha ~6 m, árvores 5–9 m, arcos de portal ~5 m.
  Os prédios v2 já vêm em tamanho real do Blender (`Art/scripts/kezor_town2.py`). Alcance do baú e da guilda 6,5 m;
  área de visão (AOI) 60 m.
- **Noite do Vale**: luar azul fraco; a luz quente vem de até 8 luzes locais (braseiros dos portões, postes, forja, fonte,
  portais) somadas no shader. Emissivo acima de 1 no buffer HDR (refino +11/+13/+15, épicas, asas de 3º nível, faíscas)
  com bloom próprio do pipeline Built-in; liga/desliga e intensidade nas Opções.
- **Bosque Cinzento**: quanto mais longe da vila, mais forte. Estradas nos 4 sentidos; cantos queimados no sudeste e noroeste.

| Monstro | Nv | HP | Onde | Comportamento |
|---|---|---|---|---|
| Lodo Musgoso | 2 | 60 | portões N/L/O | passivo |
| Lobo Cinzento | 8 | 180 | portão S, nordeste | agressivo, rápido |
| Aranha Tecelã | 14 | 330 | noroeste, sudeste | agressiva |
| Ossada Errante | 22 | 620 | sudoeste, nordeste | agressiva |
| Golem de Basalto | 35 | 1500 | cantos queimados | agressivo, lento, forte |

Drops por abate: item 12% (refino até +min(nv/10, 6), Sorte 10%, Skill 30% em armas, opção 25%, Ascendente 1%) · runa 2,5% · poção 9% · ouro sempre.
Grupo: até 5, exp dividida por nível com +15% por membro no raio de 25 u.

## Evento: Invasão das Cinzas
A cada 15 min (primeira aos 3–4 min) com aviso 30 s antes: o **Arauto das Cinzas** (nv 30, 6000 HP) e 8 **Crias das Cinzas** surgem
no portão sul. O Arauto deixa 3 runas garantidas e item com 20% de chance de Ascendente. Some após 5 min se ninguém vencer.

## Conjuntos de armadura (bônus com as 5 peças)
| Conjunto | Classes | Bônus |
|---|---|---|
| Couro | todas | defesa +5, taxa de defesa +5% |
| Escamas | Guardião, Patrulheiro | defesa +12, taxa de defesa +10% |
| Véu | Arcanista | defesa +6, poder mágico +15% |
| Placas Rúnicas | Guardião | defesa +20, −5% dano recebido, dano +5% |
| Trama Arcana | Lâmina Arcana | defesa +10, dano +6%, poder mágico +8% |
| Bastião | Comandante | defesa +18, −4% dano recebido, taxa de defesa +5% |
| Véu dos Ecos | Invocadora | defesa +8, poder mágico +12%, −2% dano recebido |
| Couro Ígneo | Punho de Brasa | defesa +8, dano +10% |

Armas novas: espadas da Lâmina (Lâmina Rúnica Curta, Sabre do Crepúsculo Arcano, Espada da Maré Prismática), lanças do Comandante
(Lança do Recruta, Lança de Vanguarda, Alabarda do Bastião), tomos e sinos da Invocadora (Tomo do Eco Menor, Sino de Prata Velada,
Grimório dos Mil Ecos) e manoplas do Punho (Couro Cru, Ferro Fundido, Punhos de Magma, usadas nas duas mãos). Épicas com rastro e
partículas: **Lâmina Astral Partida** (violeta), **Haste do Juramento Solar** (dourada), **Sino do Silêncio Eterno** (lilás) e
**Núcleo de Brasa Viva** (brasa).

## Mapas
| Mapa | Nível | Como chegar | Destaques |
|---|---|---|---|
| Vale de Brasalume | 1+ | (vila, renascer) | vila murada, baú, loja, forja, evento |
| Pântano de Turfa Negra | 20–45 | portal sudeste do Vale ou `/warp PANTANO` (30 k) | poças de turfa intransponíveis, névoa verde; Rastejante da Turfa (24), Bruxa do Junco (30), Carniçal Afogado (36), Mariposa Fátua (42) |
| Cripta de Vidro Negro | 45–80 | portal nordeste do Vale ou `/warp CRIPTA` (120 k) | salões e corredores; Sentinela de Vidro (50), Aranha de Obsidiana (58), Espectro Vítreo (66); chefe **Soberano de Vidro Negro** (78, volta a cada 4 min, sempre deixa arma épica ou asas) |

Voltar ao Vale por `/warp VALE` é grátis. Morrer em qualquer mapa leva à vila.

## Baú da cidade
Ilsa, a Guardiã (praça do Vale): 120 espaços com empilhamento e até 2 bilhões de ouro, só dentro da vila e perto dela.
O baú é da **conta**: os 3 personagens veem o mesmo. Itens vinculados não entram.

## Contas
Até 3 personagens por conta. Senha guardada só como hash PBKDF2-SHA256. No servidor dedicado é obrigatório entrar na conta;
no modo local e na LAN joga-se como convidado.

## Troca entre jogadores
Até 4 m de distância, até 12 itens e ouro de cada lado. Passo 1: travar; passo 2: confirmar. Qualquer mudança destrava os dois.
Itens vinculados não são trocados. A troca é cancelada se alguém se afastar, mudar de mapa ou cair.

## Guildas
Fundar com Hadrik, o Arauto das Guildas (nível 50, 500 000 de ouro). Até 30 membros. Cargos: **Líder** (tudo), **Oficial**
(convida e expulsa membros), **Membro**. Se o líder sair, a liderança passa a um oficial. Chat de guilda: `/g`. O nome da guilda
aparece acima do herói.

## Loja pessoal
Só dentro da vila. Até 16 itens com preço em ouro; o herói fica parado com uma placa com o nome da loja. Compradores a até 6 m
abrem a vitrine e compram; o servidor só vende se o item e o preço forem os anunciados.

## Ranking
Pontuação de herói = 400 × renascimentos + nível (desempate pela experiência). Rankings: geral, por classe e de guildas
(soma dos membros + 1000 por vitória em guerra). Tecla K.

## Guerra de guildas
O líder declara; o líder da outra guilda tem 2 minutos para aceitar. Dura 10 minutos. Cada abate entre as duas guildas fora da vila
vale 1 ponto e não dá infâmia. Vence quem fizer mais pontos (empate conta como empate); o resultado fica no histórico da guilda.

## Roteiro de conteúdo (próximos mapas, todos originais)
- **Catacumbas de Ossário** (masmorra subterrânea, nv 40–70), **Pico da Geada Eterna** (gelo, nv 60–100),
  **Dunas de Sal Rubro** (deserto, nv 90–140), **Pântano Lúgubre** (nv 130–180), **Cidadela Partida** (cidade em ruínas no céu, 200+).
- Eventos: **Vigília da Chama** (defender a Fonte em ondas), **Arena do Crepúsculo** (PvP por rodadas), cerco entre guildas.


## Inventário em grade (itens por tamanho)
Mochila 8×8 e baú 8×15. Cada item ocupa w × h casas a partir do canto superior esquerdo (sem rotação):
| Item | Tamanho |
|---|---|
| Poção, runa, cristal, mascote | 1×1 |
| Espada | 1×3 (grandes e épicas 1×4) |
| Cajado, lança | 1×4 |
| Arco | 2×4 |
| Machado | 2×3 |
| Escudo, elmo, calças, luvas, botas, manoplas, tomo/sino, montaria | 2×2 |
| Armadura | 2×3 |
| Asas | 3×2 |

Pegar, comprar, retirar do baú ou desequipar procura o primeiro lugar onde o item cabe; consumíveis empilham.
`IMOVE de para` move dentro da mochila e `VMOVE` dentro do baú; `VPUT`/`VTAKE` aceitam a casa de destino.
A troca confere se os itens recebidos cabem de verdade (simulação), e equipar desfaz se o item que sai não couber.
Saves antigos são reempacotados ao carregar; o que não couber na mochila vai para o baú.

## HUD v4: Vigor, Escudo e atalhos Q W E R
- **Vigor** (barra vertical amarela ao lado do Cristal de Vida): máximo 100 + Agilidade/10 (até 200). Cada skill gasta 6;
  o ataque básico não gasta. Volta 8 por segundo em combate e 25 fora dele (sai de combate 5 s depois do último golpe dado
  ou recebido). Sem vigor a skill é recusada pelo servidor ("vigor insuficiente") e o herói segue no ataque básico.
- **Escudo** (barra vertical azul ao lado do Cristal de Mana): 20% da vida máxima. Absorve o dano antes da vida (o número
  mostrado é o dano inteiro). Começa vazio, recarrega 10% por segundo depois de 6 s sem levar dano e zera ao cair.
- **Atalhos Q W E R**: arraste uma poção da mochila para o atalho; o número é o total na mochila. O servidor valida (só
  consumíveis, sem repetir o item em dois atalhos) e guarda os atalhos com o personagem (`QUICK n id`, `USEQ n`). Padrão:
  Poção Rubra, Poção Cerúlea, Poção Rubra Maior e vazio. A montaria passou para a tecla X.
- Base da tela: Cristal de Vida, Vigor, Q W E R, skills 1–5, **magia ativa** (a skill escolhida, usada com o botão direito),
  8 botões quadrados de menu, Escudo e Cristal de Mana; experiência em 20 segmentos de 5% com dica detalhada.

## Árvore da Ascensão (nível 400)
- **Liberação**: ao chegar ao nível 400 pela primeira vez. É **permanente**: renascer não apaga o nível de Ascensão, os pontos
  nem os nós, e os bônus continuam valendo no nível 1. A experiência de Ascensão só entra enquanto o herói está no 400.
  No 400, toda a experiência de abates vira Ascensão.
- **Curva própria**: próximo nível = 50 M + 5 M × nível atual. Máximo 200. **1 ponto por nível.**
- **Três ramos por classe**, cada um com 15 nós em 5 linhas × 3 colunas (ranks 5; última linha rank 3):
  - **Bastião** (comum): vida %, defesa, taxa de defesa, redução de dano, vigor, escudo;
  - **Fúria** (comum): dano %, taxa de ataque, crítico, vida por abate, velocidade;
  - **ramo da classe**, exclusivo: Juramento Rúnico, Profundezas do Véu, Lei da Mata, Trama da Lâmina, Legião, Coro dos
    Ecos e Forja Viva. As classes mágicas recebem poder mágico e mana; a Lâmina Arcana mistura dano e magia.
- **Regras**: cada nó pede o de cima (mesma coluna) e cada linha pede 5 pontos gastos no ramo por linha (0, 5, 10, 15, 20).
  45 nós e 207 ranks por classe, então dá para gastar os 200 pontos.
- **Protocolo**: `ASC INFO`, `ASC LEARN <nó>`, `ASC RESET` (redistribui por 500 mil de ouro por ponto gasto, mínimo 1 milhão).
  O servidor manda `@ASC liberado;nível;exp;próximo;pontos;nó:rank,...` quando algo muda. Tudo é salvo no personagem.
- **Janela**: tecla A (ou o botão com a runa no menu), em tela cheia, com os três ramos lado a lado.

## Login com lista de servidores
- Fluxo: **servidor → conta → seleção em fileira**. O fundo é a vila à noite, com a câmera passeando baixo e devagar.
- Lista: **Local** (servidor embutido, conta automática), **Hospedar LAN** (porta configurável) e os servidores do
  `servers.json`. O jogo procura primeiro em `persistentDataPath/kezor/servers.json` e depois no `StreamingAssets/servers.json`
  que vem com o jogo. Cada servidor tem nome, endereço, região e canais (um canal = uma porta). Entradas inválidas são ignoradas.
- **STATUS** (funciona sem login): `OK status nome;canal;online;máximo;versão`. O cliente faz um ping TCP em cada canal a cada
  15 s (ou no botão Atualizar) e mostra população, latência e um ponto verde, amarelo ou vermelho. Canal lotado não deixa
  continuar, e o servidor também recusa o PLAY quando está cheio.
- Lembra o último servidor, o canal e a conta (`PlayerPrefs`); **a senha nunca é salva**.
- O Menu tem Opções, Créditos (licenças OFL de Cinzel e Inter) e Sair do jogo.
- Servidor dedicado: `dotnet run -- 55901 --name "Brasalume" --channel 1 --max 300` (Build/Headless), ou os campos
  serverName/channel/maxPlayers do `ServerBootstrap`.


## Direção de arte mid poly estilizada
- Formas arredondadas e normais suaves; aresta dura só acima de 70°. Cor base na cor do vértice (com AO assada);
  a textura de detalhe procedural só quebra a uniformidade (pele, cabelo, metal, couro, tecido, madeira, pedra, reboco,
  telha, folhagem; terreno: grama, terra, calçamento, cascalho). Emissivo continua pelo alfa < 0,5 da cor do vértice.
- Ambiente vivo: grama e flores balançam com o vento, névoa rasteira nas baixadas, vagalumes à noite, brasas nos fogos.
- Orçamento: herói equipado ~20-25k tris no LOD0 (LOD1/2 automáticos); grama desenhada só num raio de ~60 m.
