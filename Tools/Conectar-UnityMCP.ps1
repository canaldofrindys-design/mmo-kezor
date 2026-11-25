<#
  Kezor - Integração automática do Unity MCP (MCP for Unity, da Coplay) com o Claude Desktop.

  O que faz (pode rodar quantas vezes quiser; não duplica nada):
    1. Verifica Git (o Unity precisa dele para baixar pacotes por URL Git) e instala via winget se faltar.
    2. Verifica o uv/uvx (roda o servidor MCP em Python) e instala se faltar.
    3. Adiciona o pacote com.coplaydev.unity-mcp ao Packages\manifest.json do projeto.
    4. Adiciona o servidor "UnityMCP" ao claude_desktop_config.json (com backup do arquivo antigo).
    5. Opcionalmente reinicia o Claude Desktop e abre o projeto no Unity.

  Uso: clique duas vezes em Conectar-UnityMCP.bat (ou rode este .ps1 no PowerShell).
#>
param(
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot),
    [string]$PackageVersion = "v10.3.0",
    [switch]$SemPerguntas
)

$ErrorActionPreference = "Stop"
$PackageName = "com.coplaydev.unity-mcp"
$PackageUrl  = "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#$PackageVersion"
$ServerName  = "UnityMCP"

function Passo($t) { Write-Host ""; Write-Host "==> $t" -ForegroundColor Cyan }
function Ok($t)    { Write-Host "    [ok] $t" -ForegroundColor Green }
function Aviso($t) { Write-Host "    [!] $t" -ForegroundColor Yellow }
function Erro($t)  { Write-Host "    [x] $t" -ForegroundColor Red }
function Perguntar($t) {
    if ($SemPerguntas) { return $true }
    $r = Read-Host "$t (S/n)"
    return ($r -eq "" -or $r -match '^[sSyY]')
}
function Salvar-Json($path, $obj) {
    $json = $obj | ConvertTo-Json -Depth 50
    [System.IO.File]::WriteAllText($path, $json, (New-Object System.Text.UTF8Encoding($false)))
}
function Atualizar-Path {
    $env:Path = [Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [Environment]::GetEnvironmentVariable("Path", "User") +
                ";$env:USERPROFILE\.local\bin;$env:USERPROFILE\.cargo\bin"
}

Write-Host "Kezor - Integração do Unity MCP" -ForegroundColor White
Write-Host "Projeto: $ProjectPath"

# ---------------------------------------------------------------- 0. projeto
$manifestPath = Join-Path $ProjectPath "Packages\manifest.json"
if (-not (Test-Path $manifestPath)) {
    Erro "Não achei $manifestPath. Rode o script de dentro da pasta Tools do projeto ou use -ProjectPath."
    exit 1
}

# ---------------------------------------------------------------- 1. Git
Passo "1/5 Git"
Atualizar-Path
if (Get-Command git -ErrorAction SilentlyContinue) { Ok (git --version) }
else {
    Aviso "Git não encontrado (o Unity precisa dele para pacotes via URL Git)."
    if ((Get-Command winget -ErrorAction SilentlyContinue) -and (Perguntar "Instalar o Git agora com o winget?")) {
        winget install --id Git.Git -e --accept-source-agreements --accept-package-agreements
        Atualizar-Path
        if (Get-Command git -ErrorAction SilentlyContinue) { Ok "Git instalado." } else { Aviso "Instalado, mas talvez seja preciso reiniciar o PC para o Unity enxergar." }
    } else { Aviso "Instale manualmente: https://git-scm.com/download/win" }
}

# ---------------------------------------------------------------- 2. uv / uvx
Passo "2/5 uv (executa o servidor MCP)"
Atualizar-Path
$uvx = Get-Command uvx -ErrorAction SilentlyContinue
if (-not $uvx) {
    Aviso "uvx não encontrado."
    if (Perguntar "Instalar o uv agora (instalador oficial da Astral)?") {
        if (Get-Command winget -ErrorAction SilentlyContinue) {
            winget install --id astral-sh.uv -e --accept-source-agreements --accept-package-agreements
        } else {
            powershell -NoProfile -ExecutionPolicy Bypass -Command "irm https://astral.sh/uv/install.ps1 | iex"
        }
        Atualizar-Path
        $uvx = Get-Command uvx -ErrorAction SilentlyContinue
    }
}
if (-not $uvx) {
    $cands = @("$env:USERPROFILE\.local\bin\uvx.exe", "$env:LOCALAPPDATA\Microsoft\WinGet\Links\uvx.exe", "$env:USERPROFILE\.cargo\bin\uvx.exe")
    foreach ($c in $cands) { if (Test-Path $c) { $uvx = Get-Item $c; break } }
}
if (-not $uvx) { Erro "Não consegui achar o uvx. Instale o uv (https://docs.astral.sh/uv/) e rode este script de novo."; exit 1 }
$uvxPath = if ($uvx -is [System.Management.Automation.CommandInfo]) { $uvx.Source } else { $uvx.FullName }
Ok "uvx: $uvxPath"
Write-Host "    Baixando o servidor MCP (primeira vez pode demorar)..."
$ErrorActionPreference = "Continue"
& $uvxPath --from mcpforunityserver mcp-for-unity --help *> $null
$code = $LASTEXITCODE
$ErrorActionPreference = "Stop"
if ($code -eq 0) { Ok "Servidor mcpforunityserver disponível." } else { Aviso "Não consegui testar o servidor agora (código $code). O Claude vai baixar na primeira execução." }

# ---------------------------------------------------------------- 3. pacote no Unity
Passo "3/5 Pacote MCP for Unity no projeto"
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$current = $manifest.dependencies.PSObject.Properties[$PackageName]
if ($current -and $current.Value -eq $PackageUrl) { Ok "Já estava no manifest.json." }
else {
    Copy-Item $manifestPath "$manifestPath.bak" -Force
    if ($current) { $current.Value = $PackageUrl } else { $manifest.dependencies | Add-Member -NotePropertyName $PackageName -NotePropertyValue $PackageUrl }
    Salvar-Json $manifestPath $manifest
    Ok "Adicionado $PackageName ($PackageVersion). Backup: manifest.json.bak"
}

# ---------------------------------------------------------------- 4. Claude Desktop
Passo "4/5 Configuração do Claude Desktop"
$configDirs = @("$env:APPDATA\Claude")
Get-ChildItem "$env:LOCALAPPDATA\Packages" -Directory -Filter "Claude_*" -ErrorAction SilentlyContinue | ForEach-Object {
    $configDirs += (Join-Path $_.FullName "LocalCache\Roaming\Claude")   # versão da Microsoft Store
}
$configDirs = $configDirs | Where-Object { Test-Path $_ }
if (-not $configDirs) { New-Item -ItemType Directory -Force "$env:APPDATA\Claude" | Out-Null; $configDirs = @("$env:APPDATA\Claude") }

$server = [pscustomobject]@{
    command = $uvxPath
    args    = @("--from", "mcpforunityserver", "mcp-for-unity", "--transport", "stdio")
}
foreach ($dir in $configDirs) {
    $cfgPath = Join-Path $dir "claude_desktop_config.json"
    if (Test-Path $cfgPath) {
        $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
        Copy-Item $cfgPath "$cfgPath.$stamp.bak" -Force
        $raw = Get-Content $cfgPath -Raw
        try { $cfg = if ($raw.Trim()) { $raw | ConvertFrom-Json } else { [pscustomobject]@{} } }
        catch { Erro "O arquivo $cfgPath não é um JSON válido. Corrija ou apague e rode de novo (backup salvo)."; continue }
    } else { $cfg = [pscustomobject]@{} }
    if (-not $cfg.PSObject.Properties["mcpServers"]) { $cfg | Add-Member -NotePropertyName mcpServers -NotePropertyValue ([pscustomobject]@{}) }
    if ($cfg.mcpServers.PSObject.Properties[$ServerName]) { $cfg.mcpServers.PSObject.Properties.Remove($ServerName) }
    $cfg.mcpServers | Add-Member -NotePropertyName $ServerName -NotePropertyValue $server
    Salvar-Json $cfgPath $cfg
    Ok "Servidor '$ServerName' gravado em $cfgPath"
}

# ---------------------------------------------------------------- 5. reiniciar e abrir
Passo "5/5 Finalizar"
$unityHubEditors = "C:\Program Files\Unity\Hub\Editor"
$unityExe = Get-ChildItem $unityHubEditors -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName "Editor\Unity.exe" } | Where-Object { Test-Path $_ } | Select-Object -First 1
$unityRunning = Get-Process -Name Unity -ErrorAction SilentlyContinue
if (-not $unityRunning -and $unityExe -and (Perguntar "Abrir o projeto no Unity agora ($unityExe)?")) {
    Start-Process $unityExe -ArgumentList "-projectPath `"$ProjectPath`""
    Ok "Unity abrindo. Ele vai baixar o pacote MCP for Unity na primeira vez."
} elseif ($unityRunning) { Aviso "O Unity já está aberto: ele importa o pacote novo ao voltar para a janela do editor." }

$claude = Get-Process -Name "Claude" -ErrorAction SilentlyContinue
if ($claude) {
    if (Perguntar "Reiniciar o Claude Desktop agora para carregar o MCP? (a conversa continua depois)") {
        $exe = ($claude | Select-Object -First 1).Path
        $claude | Stop-Process -Force
        Start-Sleep -Seconds 2
        if ($exe) { Start-Process $exe; Ok "Claude Desktop reiniciado." }
    } else { Aviso "Feche o Claude Desktop pela bandeja do sistema (Sair) e abra de novo." }
} else { Aviso "Abra o Claude Desktop para ele carregar o MCP." }

Write-Host ""
Write-Host "Pronto! No Unity, confira em Window > MCP for Unity que o status está 'Connected'." -ForegroundColor Green
Write-Host "Depois volte ao Claude e peça para usar o Unity MCP."
if (-not $SemPerguntas) { Read-Host "Enter para fechar" | Out-Null }
