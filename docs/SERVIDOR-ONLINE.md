# Servidor online 24 horas

Com o servidor num **VPS** (um computador alugado na internet, ligado o tempo todo), os amigos jogam a qualquer hora,
sem depender do seu PC. O jogo mostra o servidor na tela de login, como nos MMOs clássicos.

## 1. Alugar o VPS (uma vez)
- Qualquer empresa de VPS serve. Escolha **Ubuntu 24.04** (ou 22.04), **1 vCPU e 1 a 2 GB de RAM** (dá para dezenas de jogadores),
  de preferência com servidor no Brasil (menos ping).
- Ao criar, defina a **senha do root** e anote o **IP** que a empresa mostrar.
- Se o painel da empresa tiver "firewall" ou "security group", libere a **porta TCP 55901** (entrada).

## 2. Publicar o servidor (e sempre que quiser atualizar)
1. No seu PC, dê duplo clique em `C:\MuProtoUnity\Tools\Publicar-Servidor.bat`.
2. Na primeira vez ele pede o **IP do VPS**, a **sua conta GM** (a conta que você vai criar no jogo) e o **nome do servidor**.
   Isso fica guardado em `Tools\servidor-online.txt` (não vai para o GitHub). Para mudar, apague esse arquivo.
3. Ele instala o .NET 8 se precisar, monta o servidor para Linux, envia para o VPS e instala. Digite a senha do root quando pedir.
4. No fim ele coloca o servidor na lista do login (`Assets/StreamingAssets/servers.json`). Rode o `Enviar-GitHub.bat` para guardar.

Atualizar é igual: rode o `Publicar-Servidor.bat` de novo. Ele salva todos os jogadores, faz backup dos saves e troca só o programa.

## 3. Jogar
- Dê Play (ou abra o build): o servidor aparece no topo da lista do login com população e ping.
- Crie a conta no servidor online (marque "Criar conta nova"). A conta GM que você informou tem os comandos `/gm`.
- Para os amigos: gere o build do jogo (File ▸ Build) **depois** de publicar, para o `servers.json` com o IP ir junto, e mande a pasta do build.

## Como o servidor fica no VPS
| O quê | Onde |
|---|---|
| Programa | `/opt/kezor/bin/Kezor.Server` (já tem o .NET dentro) |
| Saves (contas, heróis, guildas, ranking) | `/opt/kezor/data` |
| Backups | `/opt/kezor/backups` (todo dia às 03:30, guarda 14 dias, e antes de cada atualização) |
| Configuração (porta, nome, contas GM) | `/etc/kezor/kezor.env` |

Ele roda como serviço `kezor-server`: liga sozinho quando o VPS liga e reinicia sozinho se cair (5 s).
Ao parar ou atualizar, ele salva todos os jogadores antes de sair. Comandos úteis (entrando no VPS com `ssh root@IP`):
- ver o que está acontecendo (entradas, saídas, avisos): `journalctl -u kezor-server -f`
- reiniciar / parar: `systemctl restart kezor-server` / `systemctl stop kezor-server`
- mudar contas GM ou nome: edite `/etc/kezor/kezor.env` e reinicie.

## Banco de dados
O servidor online guarda contas, heróis, guildas e ranking num **banco SQL**:
- **SQLite (padrão)**: um arquivo só, `/opt/kezor/data/kezor.db`. Não precisa instalar nada. Aguenta tranquilo um servidor
  com centenas de contas. O servidor faz uma cópia do banco a cada 6 horas em `/opt/kezor/data/backups` (guarda as 14 últimas).
- **MySQL / MariaDB** (como os servidores grandes): instale no VPS (`apt install mariadb-server`), crie um usuário e coloque em
  `/etc/kezor/kezor.env`: `KEZOR_DB=mysql:Server=localhost;Database=kezor;User ID=kezor;Password=SUA_SENHA`.
  O banco e as tabelas são criados sozinhos. Para backup use `mysqldump kezor > kezor.sql`.

Tabelas: `accounts` (conta, hash da senha, criação, ban), `characters` (nome, conta, classe, nível, renascimentos),
`guilds` e `ranking`. Cada linha tem o registro completo em JSON na coluna `data`. Dá para abrir com qualquer programa de SQL
(DB Browser for SQLite, HeidiSQL, DBeaver).

Saves antigos em arquivos `.json` na pasta `data` são copiados para o banco sozinhos na primeira vez (nada é apagado).
Para continuar com arquivos, use `KEZOR_DB=json`. O "Servidor deste PC" (dentro do Unity) continua usando arquivos.

## Moderação (GM) e registros

As contas da lista de GMs (`KEZOR_GM_ACCOUNTS` no `/etc/kezor/kezor.env`, ou a sua conta no "Servidor deste PC") usam estes comandos
no chat do jogo. Onde está `<nome>` pode ser o nome do herói ou da conta.

| No chat do jogo | O que faz |
|---|---|
| `/gm kick <nome> [motivo]` | derruba o jogador (o herói é salvo) |
| `/gm ban <nome> <dias> [motivo]` ou `/gm ban <nome> perm [motivo]` | bane a conta (cai na hora e não entra mais) |
| `/gm unban <conta>` | tira o banimento |
| `/gm mute <nome> [minutos]` / `/gm unmute <nome>` | bloqueia / libera o chat (dizer, sussurro, grupo, guilda) |
| `/gm info <nome>` | conta, heróis, mapa, posição, ouro, se está banida ou silenciada |
| `/gm goto <nome>` / `/gm bring <nome>` | vai até o jogador / traz o jogador até você (em qualquer mapa) |
| `/gm aviso <texto>` | aviso em destaque para todo mundo |
| `/gm rift` / `/gm event` | abre a Fenda da Geada Rubra / começa a Invasão das Cinzas |

No console do servidor (quando roda numa janela, não como serviço) os mesmos comandos existem em português:
`info`, `expulsar`, `banir <conta> <dias|perm> [motivo]`, `desbanir`, `silenciar`, `liberar`, `aviso`, `evento`, `fenda`, `online`, `salvar`, `backup`, `sair`.
Um GM não consegue banir outro GM pelo jogo; pelo console pode.

**Registro:** tudo fica em `/opt/kezor/data/logs/kezor-AAAA-MM-DD.log` (um arquivo por dia) e no `journalctl -u kezor-server`:
entradas e saídas, contas criadas, logins recusados de contas banidas, trocas entre jogadores (o que cada um deu), compras em lojas
de jogadores, itens raros pegos do chão (runas, joias, épicos, asas, itens +7), prêmios da Fenda e todo comando de GM
(inclusive `/gm level`, `/gm gold` e `/gm item`, para ninguém da equipe criar item escondido).

**Proteção contra trapaça:** o servidor decide tudo (movimento, colisão, dano, drops, ouro, trocas e lojas); o jogo só pede.
Cada conexão tem limite de 60 mensagens por segundo e linhas de no máximo 512 caracteres; o chat tem intervalo mínimo;
a troca é revalidada na hora de concluir (itens e ouro precisam continuar lá); a mesma conta não entra duas vezes;
conexão muda cai em 60 s; senhas são guardadas com PBKDF2.

## Windows em vez de Linux
Também funciona num VPS Windows: monte com
`dotnet publish Build/Headless/Kezor.Server.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o Publish`,
copie a pasta, rode `Kezor.Server.exe 55901 --gm-account SuaConta` e libere a porta 55901 no firewall do Windows.
