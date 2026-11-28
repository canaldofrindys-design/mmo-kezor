# Mecânicas do Kezor x MU Online clássico

Levantamento feito para deixar as **regras e fórmulas** do Kezor iguais às do MU Online. Nomes, arte, sons e textos
continuam do Kezor. As fórmulas do MU foram conferidas no **OpenMU** (emulador de código aberto do MU, licença MIT),
que reproduz o servidor original. Onde o OpenMU não tem o número, está marcado como **a confirmar**.

Equivalência de classes usada em tudo abaixo:

| Kezor | Papel no MU |
|---|---|
| Guardião Rúnico | Dark Knight |
| Arcanista do Véu | Dark Wizard |
| Patrulheiro Selvagem | Fairy Elf |
| Lâmina Arcana | Magic Gladiator |
| Comandante do Estandarte | Dark Lord |
| Invocadora de Ecos | Summoner |
| Punho de Brasa | Rage Fighter |

Legenda: ✅ já era igual · ✏️ muda neste PR · ⏳ fica para depois (motivo na linha)

## 1. Atributos e pontos por nível

| Classe | FOR/AGI/VIT/ENE hoje | MU | Pontos por nível hoje → MU |
|---|---|---|---|
| Guardião | 28/20/25/10 ✅ | 28/20/25/10 | 5 → 5 ✅ |
| Arcanista | 18/18/15/30 ✅ | 18/18/15/30 | 5 → 5 ✅ |
| Patrulheiro | 22/25/20/15 ✅ | 22/25/20/15 | 5 → 5 ✅ |
| Lâmina Arcana | 24/20/20/22 ✏️ | 26/26/26/26 | 5 → **7** ✏️ |
| Comandante | 24/18/26/20 ✏️ | 26/20/20/15 (+25 de Comando) | 5 → **7** ✏️ |
| Invocadora | 15/18/18/32 ✏️ | 21/21/18/23 | 5 → 5 ✅ |
| Punho de Brasa | 26/27/22/10 ✏️ | 32/27/25/20 | 5 → **7** ✏️ |

⏳ O atributo **Comando** do Dark Lord não existe no Kezor. Entra num PR próprio (mexe na tela, no protocolo e no banco).

## 2. Vida, mana e AG (Vigor)

Fórmula do MU: `valor = constante + por_nível × nível + por_atributo × atributo` (atributo total, com os pontos gastos).

| Classe | Vida | Mana | AG máximo |
|---|---|---|---|
| Guardião | 35 + 2·Nv + 3·VIT | 10 + 0,5·Nv + 1·ENE | ENE + 0,3·VIT + 0,2·AGI + 0,15·FOR |
| Arcanista | 30 + 1·Nv + 2·VIT | 2·Nv + 2·ENE | 0,2·ENE + 0,3·VIT + 0,4·AGI + 0,2·FOR |
| Patrulheiro | 39 + 1·Nv + 2·VIT | 6 + 1,5·Nv + 1,5·ENE | 0,2·ENE + 0,3·VIT + 0,2·AGI + 0,3·FOR |
| Lâmina Arcana | 57 + 1·Nv + 2·VIT | 7 + 1·Nv + 2·ENE | 0,15·ENE + 0,3·VIT + 0,25·AGI + 0,2·FOR |
| Comandante | 48,5 + 1,5·Nv + 2·VIT | 38 + 1·Nv + 1,5·(ENE−15) | 0,15·ENE + 0,1·VIT + 0,2·AGI + 0,3·FOR |
| Invocadora | 39 + 1·Nv + 2·VIT | 6 + 1,5·Nv + 1,7·ENE | 0,15·ENE + 0,3·VIT + 0,25·AGI + 0,2·FOR |
| Punho de Brasa | 57 + 1,3·Nv + 2·VIT | 7 + 1·Nv + 1,3·ENE | ENE + 0,3·VIT + 0,2·AGI + 0,15·FOR |

Hoje: as três primeiras classes estavam quase iguais (diferença de 1 a 2 pontos); as quatro novas tinham números próprios. ✏️
Hoje o Vigor máximo é 100 + AGI/10 para todos. ✏️
Gasto de AG: como no MU, as skills iniciais não gastam AG; as demais gastam 6 *(o custo de cada skill do MU: a confirmar)*.

**Recuperação** (MU): vida **não** volta sozinha (só poção, opção de item ou descansando); mana volta 3,7% do máximo a cada 3 s;
AG volta 2 por tique (+3 na zona segura) + 5% do máximo (Guardião) ou 3% (demais). Hoje o Kezor recupera 1% de vida/s (5% na vila)
e 3%/s de mana (8% na vila). ✏️ *(intervalo de 3 s do AG e da mana: a confirmar)*

## 3. Dano, defesa e taxas

| Classe | Dano físico mín ~ máx | Dano mágico | Defesa (AGI) | Taxa de ataque | Taxa de defesa |
|---|---|---|---|---|---|
| Guardião | FOR/6 ~ FOR/4 ✅ | — | AGI/3 | 5·Nv + 1,5·AGI + FOR/4 | AGI/3 |
| Arcanista | FOR/8 ~ FOR/4 ✅ | ENE/9 ~ ENE/4 ✅ | AGI/4 | igual | AGI/3 |
| Patrulheiro | FOR/14+AGI/7 ~ FOR/8+AGI/4 ✅ | — | **AGI/10** (hoje /6) | igual | **AGI/4** (hoje /3) |
| Lâmina Arcana | **FOR/6+ENE/12 ~ FOR/4+ENE/8** | ENE/9 ~ ENE/4 | **AGI/5** | igual | AGI/3 |
| Comandante | **FOR/7+ENE/14 ~ FOR/5+ENE/10** | — | **AGI/7** | **5·Nv + 2,5·AGI + FOR/6** | **AGI/7** |
| Invocadora | **(FOR+AGI)/7 ~ (FOR+AGI)/4** | ENE/9 ~ ENE/4 | AGI/3 | igual | **AGI/4** |
| Punho de Brasa | **FOR/7+VIT/15 ~ FOR/5+VIT/12** | — | **AGI/8** | **3·Nv + 1,25·AGI + FOR/6** | **AGI/10** |

**Defesa final** no MU = (defesa da AGI + defesa dos itens) ÷ 2. Hoje o Kezor não divide por 2. ✏️

**Velocidade de ataque** (MU): AGI/15 (Guardião, Lâmina), AGI/20 (Arcanista, Invocadora), AGI/50 (Patrulheiro), AGI/10 (Comandante),
AGI/9 (Punho), mais +7 da opção excelente. Hoje todos batem a cada 0,6 s (Punho 0,5 s). ✏️
*(O MU converte velocidade em animação no cliente, sem fórmula pública. O Kezor usa intervalo = 0,8 s × 50/(50+velocidade), mínimo 0,2 s: a confirmar.)*

## 4. Como um golpe é calculado

| Passo | Kezor hoje | MU |
|---|---|---|
| Chance de acerto | TA/(TA+TD), entre 5% e 95% | se TA > TD: 1 − TD/TA; senão 3% ✏️ |
| Dano da skill | % do dano + valor fixo | soma `dano` ~ `dano×1,5` ao dano base ✏️ |
| Golpe excelente | dano máx × 1,2 (10% da opção) ✅ | dano máx × 1,2 |
| Crítico | dano máx (5% da Sorte) ✅ | dano máx |
| Defesa | subtrai ✅ | subtrai |
| Alvo com TD maior que a TA do atacante (PvM) | nada | dano × 0,3 ✏️ |
| Dano mínimo | máx(1, nível/10) ✅ | máx(1, nível/10) |
| Asas | +% de dano e −% de dano recebido ✅ | igual |
| Multiplicador de skill da classe | não existe | Guardião 200% + ENE/10%, Lâmina 200%, Comandante 200% + ENE/20%, Punho 50% + ENE/10%, demais 100% ✏️ |
| Combo do Guardião | (FOR+AGI) × 1,5 | (FOR+AGI+ENE) ÷ 2 ✏️ |
| Escudo (SD) | segura dano de monstro e de jogador | só em **PvP**: 90% do golpe vai no SD e 10% na vida ✏️ |
| SD máximo | 20% da vida | 1,2·(FOR+AGI+VIT+ENE) + defesa + Nv²/30 ✏️ |
| Recarga do SD | em qualquer lugar, após 6 s | só na zona segura ✏️ |

## 5. Experiência e níveis

| | Kezor hoje | MU |
|---|---|---|
| Exp total para chegar ao nível N | por nível: 5·N²·(N+10)+100 | 10·(N+8)·(N−1)², e a partir do 256 soma 1000·(N−247)·(N−256)² ✏️ |
| Exp por monstro | valor fixo de cada monstro, cortado se você tem +10 níveis | (NvM+25)·NvM/3; se Nv > NvM+10, × (NvM+10)/Nv; monstro 65+ soma (NvM−64)·(NvM/4); depois sorteia de 100% a 150% ✏️ |
| Divisão no grupo | +15% por membro, divide por nível | base pela média de nível do grupo × membros × bônus do grupo, dividida por nível ✏️ |
| Nível máximo | 400 ✅ | 400 |
| Taxa do servidor | não existe | multiplicador do servidor (1x no oficial) ✏️ *(opção `ExperienceRate`)* |

Bônus de grupo: os valores do OpenMU são configuráveis. Uso os do MU clássico: grupo comum 2/3/4/5 membros = 160/180/200/220% e
grupo de classes todas diferentes (3+) = 230/270/300% da experiência de um monstro, divididos pelo nível de cada membro
(dois heróis do mesmo nível ganham 80% cada) *(a confirmar)*.

## 6. Itens e refino

| | Kezor hoje | MU |
|---|---|---|
| Runa da Aurora (Bless) | +0~+5 → +1, 100% ✅ | igual |
| Runa do Eclipse (Soul) | só de +6 a +8 | de **+0 a +8**, 50% (+25% com Sorte); falha: +7 e +8 voltam a +0, abaixo disso cai 1 nível ✏️ |
| Runa Vital (Life) | +4 por vez, 50%, falha tira a opção ✅ | igual |
| Refino +10~+15 (Chaos) | 60/55/50/45/40/35%, Sorte +20%, máx 75% | Sorte **+25%** ✏️ |
| Bônus por nível | +3 até +9, depois +4,+5…+9 ✅ | igual |
| Opções excelentes (armas) | 3 de 6 | + dano 2%, + velocidade de ataque 7, mana ao matar (mana/8) ✏️ |
| Opções excelentes (armaduras) | 3 de 6 | + mana máx 4%, reflete 5% do dano, +40% de zen ✏️ |
| Opções por item excelente | sempre 1 | 1, às vezes 2 *(chance da 2ª: a confirmar)* ✏️ |
| Chance de item excelente | 1% | 0,1% ✏️ |
| Asas nível 1/2/3 (dano / absorção) | 12/20/32% e 10/18/26% | 12/32/39% e 12/25/39% ✏️ *(bônus por nível de runa: a confirmar, mantido)* |
| Poções | recuperam um valor fixo | % da vida/mana máx + bônus que diminui com o nível: pequena 10% + (100−Nv), média 20% + (150−Nv), grande 30% + (200−Nv) ✏️ |
| Ouro (zen) por monstro | sorteio até o máximo do monstro, sempre | exp ganha + 7, com 50% de chance *(chance: a confirmar)* ✏️ |

## 7. PK (matar jogadores)

| | Kezor hoje | MU |
|---|---|---|
| Estados | contador de infâmia, nome vermelho | 1 morte = aviso (laranja), 2 = assassino 1º grau (vermelho), 3+ = 2º grau ✏️ |
| Duração | para sempre | cada grau dura 3 h jogando; depois cai um grau ✏️ |
| Legítima defesa | não existe | quem foi atacado primeiro pode revidar sem virar PK ✏️ *(duração de 60 s: a confirmar)* |
| Matar um assassino | dá infâmia | não dá ✏️ |
| Guerra de guilda | não dá infâmia ✅ | igual |

⏳ Queda de itens ao morrer como assassino: o próprio OpenMU ainda não implementou; fica para depois com os números confirmados.

## 8. O que já estava igual ou fica como está

- **Reset (Renascimento)**: não existe no MU oficial, é regra de servidor privado. O do Kezor (nível 400, ouro, volta ao 1 com pontos
  bônus) é o modelo comum desses servidores. ✅
- **Movimento por clique** e **ataque contínuo ao clicar no monstro**: já funcionam como no MU. ✅
- **Desgaste**: arma gasta ao atacar, armadura ao apanhar, item com pouca durabilidade rende menos. ✅
- ⏳ **Classes liberadas por nível** (no MU a Lâmina e o Comandante só abrem com um herói nível 220/250): mudaria quem pode jogar hoje.
- ⏳ **Skills**: os números de dano das skills do Kezor viraram "dano da skill" do MU (somado ao dano base e multiplicado pela classe).
  Os valores de cada skill ainda são os do Kezor.

## 9. O que muda para quem já joga

- Os heróis salvos continuam com os atributos que já tinham. Os pontos por nível novos valem dos próximos níveis em diante,
  e o renascimento volta aos atributos iniciais novos.
- A experiência guardada dentro do nível continua; como a tabela mudou, o herói pode subir de nível no próximo monstro.
- As classes deixam de ser equilibradas entre si pelo Kezor: como no MU, umas batem mais forte que outras em cada fase.
- A taxa de experiência do servidor começa em 1x, como no MU oficial. Para testar mais rápido, mude `ExperienceRate` nas opções do servidor.
