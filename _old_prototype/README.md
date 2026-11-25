# MuProto — protótipo de servidor MMO estilo MU (Unity + C#)

Conteúdo **original** inspirado no gênero (nenhum asset ou código da Webzen).

## Arquitetura
- `Assets/Scripts/Core` — regras do jogo em C# puro (`noEngineReferences`): roda no Unity **e** fora dele (CI/headless).
  - `Characters/` classes, atributos, level, reset · `Combat/` fórmulas e skills · `Items/` itens, jóias, inventário, Chaos Machine
  - `World/` monstros, drops · `Server/` GameServer autoritativo, repositório, host TCP
- `Assets/Scripts/Unity` — `ServerBootstrap` (MonoBehaviour: Pump da rede + Tick fixo 20 Hz) e `DebugClient` (IMGUI).
- `Assets/Tests/EditMode` — testes NUnit (rodam no Unity Test Runner e via `dotnet test`).
- `Build/` — csproj para compilar/testar sem Unity + servidor headless.

**Servidor autoritativo:** o cliente só envia intenções; o servidor valida classe, requisitos, mana, cooldown (0,25 s), slots, zen e rola todos os números aleatórios. Rede em threads → fila → lógica single-thread (sem locks na lógica).

## Protocolo (TCP, linhas UTF-8, porta 55901)
`HELP · CREATE <nome> <DK|DW|ELF> · LOGIN <nome> · STATS · ADD <STR|AGI|VIT|ENE> <n> · LOOK · ATTACK <monstroId> [skillId] · INV · EQUIP <slot> · JEWEL <slotJóia> <slotAlvo> · MIXPREVIEW <s1,s2,..> · MIX <s1,s2,..> · RESET · REPAIR`
Resposta: `OK ...` | `FAIL ...` (ação executada mas falhou na rolagem) | `ERR ...` (rejeitada, nada mudou).

## Como rodar
- **Unity (2021.3+):** abra a pasta, cena vazia, adicione `ServerBootstrap` (e `DebugClient` em outra instância/cena). Testes: Window ▸ General ▸ Test Runner ▸ EditMode.
- **Sem Unity:** `cd Build && dotnet test Tests/MuProto.Tests.csproj` · `dotnet run --project Headless -- 55901` · `nc localhost 55901`.

## Balanceamento (valores em código; tudo ajustável)
### Classes
| Classe | STR/AGI/VIT/ENE | HP base +lvl +VIT | MP base +lvl +ENE | Dano físico | Magia | Defesa |
|---|---|---|---|---|---|---|
| Dark Knight | 28/20/25/10 | 110 +2 +3 | 20 +0,5 +1 | STR/6 ~ STR/4 | — | AGI/3 |
| Dark Wizard | 18/18/15/30 | 60 +1 +2 | 60 +2 +2 | STR/8 ~ STR/4 | ENE/9 ~ ENE/4 | AGI/4 |
| Fairy Elf | 22/25/20/15 | 80 +1 +2 | 30 +1,5 +1,5 | STR/14+AGI/7 ~ STR/8+AGI/4 | — | AGI/6 |

5 pontos/nível · nível máx 400 · exp p/ próximo = 5·L²·(L+10)+100 · Reset: nível 400, custo 1 kk × (resets+1) zen, volta ao nível 1 com atributos base e 500 pontos × resets (máx 50).
Attack Rate = 5·Lvl + 1,5·AGI + STR/4 · Defense Rate = AGI/3.

### Combate
Acerto = AR/(AR+DR), limitado a 5–95%. Excelente (chance pelos itens): máx×1,2. Crítico (Luck = +5%): dano máx. Normal: aleatório min–máx.
Dano = base×%skill + fixo − defesa − redução%; piso = max(1, nível/10). Monstro contra-ataca a cada golpe. Exp com penalidade se jogador >10 níveis acima.

### Itens
- Bônus por nível (dano/defesa): +3 por nível até +9 (27); depois +4,+5,…,+9 → **+15 = +66**.
- Opção adicional (Jewel of Life): +4 por ponto, máx +28. Excelente: +25% do status base + opção especial.
- Requisito STR/AGI cresce 3% por nível. Durabilidade: base +1/nível até +9, +2/nível acima; <20% → status pela metade; 0 → item não conta.
| Jóia | Faixa | Sucesso | Falha |
|---|---|---|---|
| Bless | +0→+6 | 100% | — |
| Soul | +6→+9 | 50% (75% c/ Luck) | +6→+5; +7/+8→+0 |
| Life | opção +4→+28 | 50% | opção zerada |

### Chaos Machine
| Alvo | Base | c/ Luck | Bless+Soul | Chaos | Zen |
|---|---|---|---|---|---|
| +10 | 60% | 75% | 1+1 | 1 | 2 kk |
| +11 | 55% | 75% | 2+2 | 1 | 4 kk |
| +12 | 50% | 70% | 3+3 | 1 | 6 kk |
| +13 | 45% | 65% | 4+4 | 1 | 8 kk |
| +14 | 40% | 60% | 5+5 | 1 | 10 kk |
| +15 | 35% | 55% | 6+6 | 1 | 12 kk |
Falha: item destruído (`ChaosFailurePolicy.ResetToZero` como alternativa). Jóias sempre consumidas.
Probabilidade de levar +9 a +15 sem Luck ≈ 0,6·0,55·0,5·0,45·0,4·0,35 ≈ **1,0%** (com Luck ≈ 5,8%) → +15 é raro de propósito.
**Arma do Caos:** itens +4~+8 + 1 Chaos (+Bless 8%/Soul 5% cada); taxa = Σ(nível·4 + opção·3 + Luck 5), máx 80%; custo 10 k × taxa.

### Drops (por kill)
Item 12% (nível 0..min(lvlMonstro/10, 6), Luck 10%, Skill 30% armas, opção 25%, excelente 1%) · Jóia 2,5% (Chaos 40%, Life/Soul/Bless 20% cada).

## Próximos passos
Persistência real (SQLite/PostgreSQL via `ICharacterRepository`), autenticação de conta, movimento/AOI em grade com validação de velocidade, protocolo binário com opcodes, party/guild, PvP, NPCs/lojas, sockets e ancient sets, inventário em grade.
