# Kezor: guia visual da interface

O estilo da interface é **original do Kezor**: fantasia sombria de **brasa e cinza**, com metal envelhecido e cantos
chanfrados (o mesmo "low poly" dos modelos). Nada imita a interface de outro jogo.

## Princípios
1. **O mundo vem primeiro.** O HUD fica nas bordas, com transparência; o centro da tela é do herói.
2. **Uma cor de destaque.** Brasa/âmbar indica foco, seleção e o que pede ação. Raridade e runas têm cores próprias e só
   aparecem em itens.
3. **Facetas, não curvas.** Molduras, botões e slots têm cantos chanfrados de 45°. As únicas formas redondas são as
   orbes e o minimapa.
4. **Legível em movimento.** Números de dano e barras usam a fonte de texto (Inter) com contorno escuro. A Cinzel fica
   só nos títulos.

## Paleta
| Papel | Token USS | Cor |
|---|---|---|
| Carvão 0 (fundo mais escuro) | `--coal-0` | `#0d0b0c` |
| Carvão 1 | `--coal-1` | `#16120f` |
| Carvão 2 (fundo dos painéis) | `--coal-2` | `#241c16` |
| Carvão 3 (realce de painel) | `--coal-3` | `#33281f` |
| Ferro escuro / ferro / ferro claro | `--iron-d` `--iron` `--iron-l` | `#2b241f` `#4a3f36` `#6e6052` |
| Bronze escuro / bronze / bronze claro (aros) | `--bronze-d` `--bronze` `--bronze-l` | `#4e3b24` `#8a6a3e` `#c49a5a` |
| **Brasa** (destaque) | `--ember` | `#ff6a1a` |
| **Âmbar** (seleção, títulos ativos) | `--amber` | `#ffb347` |
| Ouro (títulos, moedas) | `--gold` | `#e8c27a` |
| Texto principal / secundário / apagado / desabilitado | `--text` `--text-2` `--text-3` `--text-off` | `#efe6d4` `#b9ad97` `#7d7366` `#55504a` |
| Vida | `--hp` | `#c8261c` (líquido) · `#ff5a3a` (brilho) |
| Mana | `--mp` | `#2457c8` (líquido) · `#6ab0ff` (brilho) |
| Experiência | `--exp` | `#d4a94a` |
| Sucesso / perigo / informação | `--ok` `--danger` `--info` | `#8fe08f` `#ff6a5a` `#8fd0ff` |

**Raridade e runas** (as mesmas cores usadas no jogo):
| | Cor |
|---|---|
| Comum | `#e8e2d0` |
| Refinado +7 ou mais | `#ffd36a` |
| Ascendente | `#6dff9e` |
| Épico (armas com rastro) | `#ff8a3a` |
| Runa da Aurora / do Eclipse / Vital / Cristal do Vórtice | cores de `Models.RuneColor` |
| Brilho de runa +7 / +9 / +11 / +13 / +15 | `#bfe3ff` `#ffd36a` `#ff8a2a` `#a77bff` `#7af0ff` |

## Tipografia
Fontes de licença livre **SIL OFL 1.1**, incluídas em `Assets/Resources/Fonts` com os arquivos de licença:
- **Cinzel** (títulos de janela, nome do jogo, nomes de classe, chefes): 16–22 px; logotipo de 64–96 px.
- **Inter** (texto, números, botões, chat): corpo 14 px, secundário 12 px, legendas 11 px, números de dano 18–30 px.

Regras: títulos em CAIXA ALTA só no logotipo; números alinhados à direita nas tabelas; nunca use menos de 11 px na
referência de 1600×900.

## Espaçamento e escala
- Grade de **4 px**: espaçamentos de 4, 8, 12, 16 e 24.
- Molduras: aro de 3 px, chanfro de 6–7 px; painel com padding de 12 px; slot de 52 px (grade) ou 56 px (barra de skills).
- **Escala por resolução**: a referência é 1600×900, casando pela altura. 720p fica em 0,8×, 1080p em 1,2×, 1440p em
  1,6× e 4K em 2,4×. A opção **Escala da interface** (80% a 140%) multiplica esse valor.

## Estados
| Estado | Botão | Slot |
|---|---|---|
| Normal | ferro com aro de bronze | poço escuro com aro de bronze |
| Hover | ferro mais claro, aro bronze claro, brilho de brasa embaixo | aro bronze claro |
| Pressionado | ferro escuro, aro âmbar | — |
| Selecionado / ativo | aro âmbar com brilho | aro âmbar 2 px com brilho |
| Desabilitado | cinza sem brilho, texto `--text-off` | ícone a 40% |
| Sem mana | — | ícone tingido de azul |
| Em recarga | — | setor radial escuro que encolhe, com os segundos restantes |

## Componentes
- **Grade de inventário**: casas de 40 px; cada item ocupa w × h casas com o ícone no formato da grade; ao arrastar,
  a área de destino fica verde (cabe) ou vermelha (não cabe).
- **Painel / janela**: moldura facetada com faixa de título (metal escuro e filete de brasa). As janelas arrastam pelo
  título, fecham no X ou com **Esc**, e a posição fica salva.
- **Orbes de vida e mana**: líquido animado com onda e brilho de vidro, dentro de um anel de 16 facetas com 4 gemas de brasa.
- **Barra única na base** (convenção clássica do gênero, arte própria): chapa de carvão com friso de bronze rebitado e filete de brasa,
  orbes nos berços das pontas, 6 skills com recarga radial e as poções Q e W no centro, menu compacto dos dois lados e
  experiência segmentada embaixo.
- **Barra de experiência**: segmentada em 10 partes, com a porcentagem.
- **Minimapa**: redondo, com anel de bronze, marcador do norte e o nome do mapa.
- **Quadro do alvo**: nome, nível, vida em número e barra; chefes ganham moldura âmbar.
- **Buffs**: ícone com timer embaixo; as auras de estandarte aparecem com borda verde e sem timer.
- **Toasts**: faixa hexagonal no alto da tela para eventos (nível, invasão, guerra, convites).
- **Tooltip de item**: nome na cor da raridade, nível de runa, opções, bônus de conjunto e comparação com o equipado
  (verde se melhora, vermelho se piora).

## Arte
Toda a arte da interface é desenhada em código (`UiArtBaker`, menu **Kezor ▸ Gerar arte da UI**) e gravada em
`Assets/Resources/UI/Art`. Os ícones de itens e skills vêm de `Kezor ▸ Gerar ícones` (atlas em `Resources/UI/Icons`).
O tema fica em `Assets/Resources/UI/Kezor.uss`; as cores acima existem como variáveis USS.

## Arquitetura
- `Kezor.Core.Net.ClientState`: dados que chegam do servidor (sem Unity).
- `KezorGame` (cliente): lógica, entrada e envio de comandos.
- `Kezor.Client.UI`: só apresentação (UI Toolkit). As views leem o `ClientState` e chamam métodos do `KezorGame`; elas
  não falam com o servidor por conta própria.
