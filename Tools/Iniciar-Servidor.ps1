# Liga o servidor dedicado do Kezor neste PC (o mesmo que roda no VPS), sem precisar do Unity aberto.
# Uso: duplo clique em Iniciar-Servidor.bat. A janela que abrir E o servidor: deixe aberta enquanto jogam.
# Para desligar: digite "sair" na janela (salva todo mundo) ou feche a janela.
# Depois: abra o jogo, escolha "Servidor deste PC" e entre com a sua conta. Amigos na mesma rede usam "Entrar por IP".

$ErrorActionPreference = 'Continue'
$Projeto = Split-Path -Parent $PSScriptRoot
$Config  = Join-Path $PSScriptRoot 'servidor-local.txt'
$Saida   = Join-Path $Projeto 'Build\Publish\local'
$Unity   = Join-Path $env:USERPROFILE 'AppData\LocalLow\DefaultCompany\MuProtoUnity\kezor'
$Dados   = Join-Path $Unity 'servidor-dedicado'
$Porta   = 55901

function Passo($t) { Write-Host ""; Write-Host "==> $t" -ForegroundColor Cyan }
function Falha($t) { Write-Host ""; Write-Host "ERRO: $t" -ForegroundColor Red; Read-Host "Aperte Enter para fechar"; exit 1 }

# 1. .NET 8 SDK (para montar o servidor)
Passo "Verificando o .NET 8"
$temSdk = $false
if (Get-Command dotnet -ErrorAction SilentlyContinue) { $temSdk = (dotnet --list-sdks | Select-String '^8\.') -ne $null }
if (-not $temSdk) {
    Write-Host ".NET 8 SDK nao encontrado. Instalando pelo winget (vai pedir confirmacao)..."
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) { Falha "winget nao existe neste Windows. Instale o .NET 8 SDK em https://dotnet.microsoft.com/download e rode de novo." }
    winget install --id Microsoft.DotNet.SDK.8 -e --source winget --accept-package-agreements --accept-source-agreements
    $env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User')
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Falha "O .NET foi instalado, mas precisa abrir uma janela nova. Feche esta e rode de novo." }
}

# 2. Conta GM (guardada em Tools\servidor-local.txt, que nao vai para o GitHub)
$gm = ''
if (Test-Path $Config) { foreach ($l in Get-Content $Config) { if ($l -match '^gm=(.*)$') { $gm = $Matches[1].Trim() } } }
else {
    $gm = (Read-Host "Sua conta no jogo que vai ser GM (pode deixar vazio)").Trim()
    Set-Content $Config @("gm=$gm") -Encoding ASCII
}

# 3. Porta livre? (o jogo com "Servidor deste PC" aberto ja usa a mesma porta)
$ocupada = Get-NetTCPConnection -LocalPort $Porta -State Listen -ErrorAction SilentlyContinue
if ($ocupada) { Falha "A porta $Porta ja esta em uso (o jogo esta aberto como servidor, ou outro servidor ja esta ligado). Feche o outro e rode de novo." }

# 4. Monta o servidor (so recompila o que mudou)
Passo "Montando o servidor"
dotnet build (Join-Path $Projeto 'Build\Headless\Kezor.Server.csproj') -c Release -o $Saida --nologo -v q
if ($LASTEXITCODE -ne 0) { Falha "A montagem do servidor falhou. Tire um print e mande para o Claude." }

# 5. Primeira vez: traz os herois do "Servidor deste PC" do jogo (os arquivos originais ficam onde estao)
if (-not (Test-Path $Dados)) {
    New-Item -ItemType Directory -Force $Dados | Out-Null
    $doJogo = Join-Path $Unity 'servidor'
    if (Test-Path (Join-Path $doJogo 'accounts')) {
        Copy-Item (Join-Path $doJogo '*') $Dados -Recurse -Force
        Write-Host "  Contas e herois do 'Servidor deste PC' copiados para o servidor dedicado."
    }
}

# 6. Liga (a janela vira o console do servidor: ajuda, online, aviso, banir, sair...)
Passo "Servidor ligado na porta $Porta"
Write-Host "  Agora abra o jogo e escolha 'Servidor deste PC'. Amigos na mesma rede: 'Entrar por IP' com o IP deste PC."
Write-Host "  Saves em $Dados"
Write-Host "  Para desligar com seguranca, digite: sair"
$serv = @("$Porta", '--data', $Dados, '--name', "Servidor de $env:USERNAME")
if ($gm -ne '') { $serv += @('--gm-account', $gm) }
& (Join-Path $Saida 'Kezor.Server.exe') @serv
Read-Host "Servidor desligado. Aperte Enter para fechar"
