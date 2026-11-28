# Monta o servidor Kezor para Linux e instala (ou atualiza) no seu VPS, que fica ligado 24h.
# Uso: duplo clique em Publicar-Servidor.bat. Na primeira vez pede o IP do VPS e a sua conta GM.
# Precisa: um VPS com Ubuntu 22.04/24.04 e a senha do usuario root (veja docs/SERVIDOR-ONLINE.md).
# Pode rodar de novo sempre que quiser atualizar o servidor: os saves dos jogadores ficam no VPS e nao sao apagados.

$ErrorActionPreference = 'Continue'
$Projeto = Split-Path -Parent $PSScriptRoot
$Config  = Join-Path $PSScriptRoot 'servidor-online.txt'
$Saida   = Join-Path $Projeto 'Build\Publish\linux-x64'

function Passo($t) { Write-Host ""; Write-Host "==> $t" -ForegroundColor Cyan }
function Falha($t) { Write-Host ""; Write-Host "ERRO: $t" -ForegroundColor Red; Read-Host "Aperte Enter para fechar"; exit 1 }

# 1. .NET SDK (para montar o servidor)
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
if (-not (Get-Command ssh -ErrorAction SilentlyContinue)) { Falha "O comando ssh nao existe. No Windows: Configuracoes > Aplicativos > Recursos opcionais > Adicionar 'Cliente OpenSSH'." }

# 2. Dados do VPS (guardados em Tools\servidor-online.txt, que nao vai para o GitHub)
Passo "Dados do seu VPS"
$ip = ''; $gm = ''; $nome = 'Kezor'
if (Test-Path $Config) {
    foreach ($l in Get-Content $Config) {
        if ($l -match '^ip=(.*)$') { $ip = $Matches[1].Trim() }
        if ($l -match '^gm=(.*)$') { $gm = $Matches[1].Trim() }
        if ($l -match '^nome=(.*)$') { $nome = $Matches[1].Trim() }
    }
}
if ($ip -eq '') {
    $ip = (Read-Host "IP do VPS (ex.: 203.0.113.10)").Trim()
    $gm = (Read-Host "Sua conta no jogo que vai ser GM (pode deixar vazio)").Trim()
    $n  = (Read-Host "Nome do servidor na tela de login [Kezor]").Trim()
    if ($n -ne '') { $nome = $n }
    if ($ip -eq '' -or $ip -match '\s') { Falha "IP invalido." }
    Set-Content $Config @("ip=$ip", "gm=$gm", "nome=$nome") -Encoding ASCII
}
Write-Host "  VPS: $ip   GM: $gm   Nome: $nome   (para mudar, apague Tools\servidor-online.txt)"

# 3. Monta o servidor (Linux, ja com o .NET dentro: o VPS nao precisa instalar nada)
Passo "Montando o servidor para Linux"
if (Test-Path $Saida) { Remove-Item $Saida -Recurse -Force }
dotnet publish (Join-Path $Projeto 'Build\Headless\Kezor.Server.csproj') -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o $Saida
if ($LASTEXITCODE -ne 0) { Falha "A montagem do servidor falhou. Tire um print e mande para o Claude." }
Copy-Item (Join-Path $Projeto 'Build\Deploy\instalar-servidor.sh') $Saida
Copy-Item (Join-Path $Projeto 'Build\Deploy\kezor-server.service') $Saida
# scripts do Linux precisam de fim de linha LF
foreach ($f in 'instalar-servidor.sh','kezor-server.service') {
    $p = Join-Path $Saida $f
    $t = [IO.File]::ReadAllText($p) -replace "`r`n", "`n"
    [IO.File]::WriteAllText($p, $t, (New-Object Text.UTF8Encoding $false))
}

# 4. Envia e instala (pede a senha do root algumas vezes)
Passo "Enviando para o VPS (digite a senha do root quando pedir)"
ssh -o StrictHostKeyChecking=accept-new "root@$ip" "rm -rf /tmp/kezor-upload && mkdir -p /tmp/kezor-upload"
if ($LASTEXITCODE -ne 0) { Falha "Nao consegui entrar no VPS. Confira o IP e a senha do root." }
scp -r "$Saida\*" "root@${ip}:/tmp/kezor-upload/"
if ($LASTEXITCODE -ne 0) { Falha "O envio dos arquivos falhou." }
Passo "Instalando no VPS"
ssh "root@$ip" "KEZOR_GM_ACCOUNTS='$gm' KEZOR_NAME='$nome' bash /tmp/kezor-upload/instalar-servidor.sh /tmp/kezor-upload"
if ($LASTEXITCODE -ne 0) { Falha "A instalacao no VPS falhou. Tire um print desta janela e mande para o Claude." }

# 5. Coloca o servidor na lista do login do jogo
Passo "Colocando '$nome' na lista de servidores do jogo"
$lista = Join-Path $Projeto 'Assets\StreamingAssets\servers.json'
try {
    $json = Get-Content $lista -Raw -Encoding UTF8 | ConvertFrom-Json
    # o servidor principal ("kezor") passa a apontar para o VPS: o jogo ja conecta nele, sem digitar IP
    $outros = @($json.servers | Where-Object { $_.id -ne 'kezor' -and $_.id -ne 'kezor-online' })
    $novo = [pscustomobject]@{ id = 'kezor'; name = $nome; host = $ip; region = 'BR'; channels = @([pscustomobject]@{ name = 'Canal 1'; port = 55901 }) }
    $json.servers = @($novo) + $outros
    [IO.File]::WriteAllText($lista, ($json | ConvertTo-Json -Depth 6), (New-Object Text.UTF8Encoding $false))
    Write-Host "  pronto: o jogo mostra '$nome' no login."
} catch { Write-Host "  nao consegui editar o servers.json: $_" -ForegroundColor Yellow }

Write-Host ""
Write-Host "PRONTO! O servidor '$nome' esta online em $ip, porta 55901." -ForegroundColor Green
Write-Host "Rode o Enviar-GitHub.bat para guardar a lista de servidores nova, e gere o build do jogo para os amigos."
Read-Host "Aperte Enter para fechar"
