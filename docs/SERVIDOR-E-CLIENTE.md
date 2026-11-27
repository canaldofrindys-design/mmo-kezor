# Servidor e cliente

O Kezor é só multiplayer: **o servidor manda em tudo** (movimento, combate, drops, ouro, trocas) e o jogo (cliente) só pede
e mostra. Os dois conversam por TCP, na porta **55901**. Dá para ligar o servidor de três jeitos, e o jogo entra em qualquer um
deles do mesmo modo.

| Jeito | Para quê | Como |
|---|---|---|
| **Servidor dedicado neste PC** | jogar com amigos ou testar com o servidor separado do jogo | duplo clique em `Tools\Iniciar-Servidor.bat` |
| **Servidor deste PC (dentro do jogo)** | testar rápido, sozinho | no login, escolha "Servidor deste PC" sem nada ligado |
| **Servidor online 24h (VPS)** | servidor de verdade para todo mundo | `Tools\Publicar-Servidor.bat` (guia em [SERVIDOR-ONLINE.md](SERVIDOR-ONLINE.md)) |

## Ligar o servidor dedicado neste PC
1. Dê duplo clique em `C:\MuProtoUnity\Tools\Iniciar-Servidor.bat`.
   - Na primeira vez ele instala o .NET 8 (se faltar) e pergunta a sua conta GM. A resposta fica em `Tools\servidor-local.txt`.
   - Também na primeira vez, ele copia as contas e os heróis do "Servidor deste PC" do jogo. Os arquivos originais não são apagados.
2. Vai abrir uma janela preta escrito **"Servidor ligado na porta 55901"**. **Essa janela é o servidor**: deixe aberta.
3. Abra o jogo (Play no Unity ou o build), escolha **Servidor deste PC** e entre com a sua conta.
   O jogo percebe que já tem um servidor ligado na porta e só conecta nele.
4. Amigos na mesma rede: no login, escolha **Entrar por IP** e digite o IP do seu PC (aparece na tela do "Servidor deste PC").
   De fora de casa, é preciso liberar a porta 55901 no roteador, ou usar o VPS.
5. Para desligar, digite `sair` na janela, ou só feche a janela. Nos dois casos todo mundo é salvo antes.

Na janela do servidor dá para digitar `ajuda`, `online`, `aviso <texto>`, `info <nome>`, `expulsar`, `banir`, `silenciar`,
`evento`, `fenda`, `backup` e `sair`. Os saves (banco SQLite) e o registro diário ficam em
`%USERPROFILE%\AppData\LocalLow\DefaultCompany\MuProtoUnity\kezor\servidor-dedicado`.

> O "Servidor deste PC" de dentro do jogo e o servidor dedicado têm saves separados. Escolha um dos dois para jogar de verdade
> (o recomendado é o dedicado).

## O que deixa a conexão robusta
- **Versão conferida na entrada:** o jogo se apresenta com a versão (`HELLO`). Se o jogo estiver desatualizado, a tela de
  login avisa "Atualize o jogo" em vez de dar erros estranhos.
- **Volta sozinho depois de uma queda:** se a internet piscar, o jogo tenta voltar (6 vezes, em até ~30 s) sem pedir a senha
  de novo, e coloca o mesmo herói no mundo. Para isso o servidor manda um código de uso único depois do login. O código vale
  por 10 minutos depois da queda e muda a cada volta.
- **Conexão fantasma:** se o servidor ainda achar que a conexão antiga está viva (Wi-Fi trocou, cabo puxado), a volta com o
  código fecha a antiga, salva o herói e entra. Com a senha, a mesma conta continua sem poder entrar duas vezes.
- **Motivo de saída:** antes de fechar uma conexão, o servidor diz o porquê (expulso por GM, conta banida, servidor desligando,
  inatividade, conexão lenta). O jogo mostra o motivo e, nesses casos, não tenta voltar sozinho.
- **Um jogador lento não trava os outros:** cada conexão tem a própria fila de envio, escrita fora da thread do jogo.
  Se alguém não consegue receber (internet muito ruim), só ele cai.
- **Limites contra abuso:** no máximo 10 conexões por IP e 1000 no total; 60 mensagens por segundo; linhas de até 512
  caracteres. Depois de 5 senhas erradas em 1 minuto do mesmo IP, é preciso esperar 30 s (fica no registro).
- **Conexão muda cai:** sem sinal do jogo por 60 s, a conexão cai e o herói é salvo. O jogo manda `PING` a cada 10 s, e o
  keep-alive do TCP está ligado nos dois lados.
- **Desligar com segurança:** `sair`, Ctrl+C, fechar a janela ou o serviço do Linux parando: todos são salvos e avisados.
  Além disso, o servidor salva sozinho a cada 60 s.

## Protocolo (para quem for mexer no código)
Linhas de texto UTF-8. O cliente manda um comando por linha. O servidor responde com uma linha `OK ...`, `ERR ...` ou `FAIL ...`
(na mesma ordem dos comandos) e empurra eventos que começam com `@` (`@S` snapshot, `@EV` evento, `@ME`, `@INV`, `@TOKEN`,
`@BYE`...). Entrada: `HELLO <versão>` → `REGISTER`/`AUTH <conta> <senha>` (ou `RESUME <conta> <código>`) → `CHARS` →
`PLAY <herói>`. `STATUS` funciona sem login (lista de servidores). Código: `Assets/Scripts/Core/Server/TcpServerHost.cs`
(rede do servidor), `GameServer.Session.cs` (entrada, volta e freio de senha), `Assets/Scripts/Core/Net/Connections.cs`
(rede do cliente) e `Assets/Scripts/Client/KezorGame.cs` (reconexão).
