<#
  Kezor - Integração automática do Blender MCP (MCP for Blender, projeto ahujasid/blender-mcp) com o Claude Desktop.

  O que faz (pode rodar quantas vezes quiser; não duplica nada):
    1. Encontra o Blender instalado (Program Files, Steam ou caminho passado em -BlenderExe).
    2. Verifica o uv/uvx (roda o servidor MCP) e instala se faltar.
    3. Instala o addon "MCP for Blender" na pasta de addons do Blender e o ATIVA (salvando as preferências).
    4. Adiciona o servidor "blender" ao claude_desktop_config.json (backup do arquivo antigo).
    5. Opcionalmente reinicia o Claude Desktop e abre o Blender.

  Uso: clique duas vezes em Conectar-BlenderMCP.bat (ou rode este .ps1 no PowerShell).
  Observação: só pode haver UM cliente usando o servidor do Blender por vez (porta 9876).
#>
param(
    [string]$BlenderExe = "",
    [switch]$SemPerguntas
)

$ErrorActionPreference = "Stop"
$ServerName = "blender"
$OldNames   = @("blender-mcp", "blender_mcp", "BlenderMCP")   # entradas antigas (ex.: venv manual) que serão substituídas

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

Write-Host "Kezor - Integração do Blender MCP" -ForegroundColor White

# ---------------------------------------------------------------- 1. Blender
Passo "1/5 Blender"
if (-not $BlenderExe) {
    $roots = @("$env:ProgramFiles\Blender Foundation", "${env:ProgramFiles(x86)}\Blender Foundation",
               "${env:ProgramFiles(x86)}\Steam\steamapps\common\Blender", "$env:ProgramFiles\Steam\steamapps\common\Blender",
               "$env:LOCALAPPDATA\Programs\Blender Foundation")
    $found = foreach ($r in $roots) {
        if (Test-Path $r) { Get-ChildItem $r -Recurse -Depth 2 -Filter blender.exe -ErrorAction SilentlyContinue }
    }
    $found = $found | Sort-Object { $_.VersionInfo.FileVersionRaw } -Descending
    if ($found) { $BlenderExe = ($found | Select-Object -First 1).FullName }
}
if ($BlenderExe -and (Test-Path $BlenderExe)) {
    $ver = (Get-Item $BlenderExe).VersionInfo.ProductVersion
    Ok "Blender: $BlenderExe ($ver)"
} else {
    Aviso "Blender não encontrado. Instale de https://www.blender.org/download/ (ou passe -BlenderExe 'C:\caminho\blender.exe')."
    if ((Get-Command winget -ErrorAction SilentlyContinue) -and (Perguntar "Instalar o Blender agora com o winget?")) {
        winget install --id BlenderFoundation.Blender -e --accept-source-agreements --accept-package-agreements
        $BlenderExe = (Get-ChildItem "$env:ProgramFiles\Blender Foundation" -Recurse -Depth 2 -Filter blender.exe -ErrorAction SilentlyContinue | Select-Object -First 1).FullName
        if ($BlenderExe) { Ok "Blender instalado: $BlenderExe" } else { Erro "Instalação não encontrada. Rode o script de novo depois de instalar."; exit 1 }
    } else { exit 1 }
}

# ---------------------------------------------------------------- 2. uv / uvx
Passo "2/5 uv (executa o servidor MCP)"
Atualizar-Path
$uvx = Get-Command uvx -ErrorAction SilentlyContinue
if (-not $uvx) {
    Aviso "uvx não encontrado."
    if (Perguntar "Instalar o uv agora?") {
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
    foreach ($c in @("$env:USERPROFILE\.local\bin\uvx.exe", "$env:LOCALAPPDATA\Microsoft\WinGet\Links\uvx.exe", "$env:USERPROFILE\.cargo\bin\uvx.exe")) {
        if (Test-Path $c) { $uvx = Get-Item $c; break }
    }
}
if (-not $uvx) { Erro "Não consegui achar o uvx. Instale o uv (https://docs.astral.sh/uv/) e rode de novo."; exit 1 }
$uvxPath = if ($uvx -is [System.Management.Automation.CommandInfo]) { $uvx.Source } else { $uvx.FullName }
Ok "uvx: $uvxPath"

# ---------------------------------------------------------------- 3. addon
Passo "3/5 Addon MCP for Blender"
Write-Host "    Baixando o servidor e instalando o addon (primeira vez pode demorar)..."
& $uvxPath mcp-for-blender install-addon
if ($LASTEXITCODE -ne 0) {
    Aviso "install-addon falhou (código $LASTEXITCODE). Tentando baixar o addon.py direto do GitHub..."
    $verDir = (Get-ChildItem "$env:APPDATA\Blender Foundation\Blender" -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1)
    if (-not $verDir) {
        $mm = ((Get-Item $BlenderExe).VersionInfo.ProductVersion -split '\.')[0..1] -join '.'
        $verDir = New-Item -ItemType Directory -Force "$env:APPDATA\Blender Foundation\Blender\$mm"
    }
    $addonsDir = Join-Path $verDir.FullName "scripts\addons"
    New-Item -ItemType Directory -Force $addonsDir | Out-Null
    Invoke-WebRequest "https://raw.githubusercontent.com/ahujasid/blender-mcp/main/addon.py" -OutFile (Join-Path $addonsDir "blender_mcp.py") -UseBasicParsing
    Ok "Addon salvo em $addonsDir\blender_mcp.py"
}

Write-Host "    Ativando o addon no Blender (modo em segundo plano)..."
$py = @'
import bpy, addon_utils, sys
mods = [m.__name__ for m in addon_utils.modules() if "mcp" in m.__name__.lower() and "blender" in m.__name__.lower()]
name = "blender_mcp" if "blender_mcp" in mods else (mods[0] if mods else None)
if not name:
    print("KEZOR: addon nao encontrado"); sys.exit(2)
addon_utils.enable(name, default_set=True, persistent=True)
bpy.ops.wm.save_userpref()
print("KEZOR: addon ativado: " + name)
'@
$tmp = Join-Path $env:TEMP "kezor_enable_blendermcp.py"
[System.IO.File]::WriteAllText($tmp, $py, (New-Object System.Text.UTF8Encoding($false)))
$ErrorActionPreference = "Continue"   # o Blender escreve no stderr; no PowerShell 5.1 isso não pode virar erro fatal
$out = & $BlenderExe --background --python $tmp 2>&1 | Out-String
$ErrorActionPreference = "Stop"

if ($out -match "KEZOR: addon ativado: (\S+)") { Ok "Addon ativado ($($Matches[1]))." }
else { Aviso "Não consegui ativar sozinho. No Blender: Edit > Preferences > Add-ons > ative 'MCP for Blender'."; Write-Host ($out -split "`n" | Select-Object -Last 8 | Out-String) }
Remove-Item $tmp -ErrorAction SilentlyContinue

# ---------------------------------------------------------------- 4. Claude Desktop
Passo "4/5 Configuração do Claude Desktop"
$configDirs = @("$env:APPDATA\Claude")
Get-ChildItem "$env:LOCALAPPDATA\Packages" -Directory -Filter "Claude_*" -ErrorAction SilentlyContinue | ForEach-Object {
    $configDirs += (Join-Path $_.FullName "LocalCache\Roaming\Claude")   # versão da Microsoft Store
}
$configDirs = @($configDirs | Where-Object { Test-Path $_ })
if (-not $configDirs) { New-Item -ItemType Directory -Force "$env:APPDATA\Claude" | Out-Null; $configDirs = @("$env:APPDATA\Claude") }

$server = [pscustomobject]@{ command = $uvxPath; args = @("mcp-for-blender") }
foreach ($dir in $configDirs) {
    $cfgPath = Join-Path $dir "claude_desktop_config.json"
    if (Test-Path $cfgPath) {
        Copy-Item $cfgPath "$cfgPath.$(Get-Date -Format 'yyyyMMdd-HHmmss').bak" -Force
        $raw = Get-Content $cfgPath -Raw
        try { $cfg = if ($raw.Trim()) { $raw | ConvertFrom-Json } else { [pscustomobject]@{} } }
        catch { Erro "O arquivo $cfgPath não é um JSON válido. Corrija ou apague e rode de novo (backup salvo)."; continue }
    } else { $cfg = [pscustomobject]@{} }
    if (-not $cfg.PSObject.Properties["mcpServers"]) { $cfg | Add-Member -NotePropertyName mcpServers -NotePropertyValue ([pscustomobject]@{}) }
    foreach ($n in @($ServerName) + $OldNames) {
        if ($cfg.mcpServers.PSObject.Properties[$n]) {
            if ($n -ne $ServerName) { Aviso "Substituindo a entrada antiga '$n'." }
            $cfg.mcpServers.PSObject.Properties.Remove($n)
        }
    }
    $cfg.mcpServers | Add-Member -NotePropertyName $ServerName -NotePropertyValue $server
    Salvar-Json $cfgPath $cfg
    Ok "Servidor '$ServerName' gravado em $cfgPath"
}

# ---------------------------------------------------------------- 5. finalizar
Passo "5/5 Finalizar"
if (-not (Get-Process -Name blender -ErrorAction SilentlyContinue) -and (Perguntar "Abrir o Blender agora?")) {
    Start-Process $BlenderExe
    Ok "Blender abrindo. O addon liga o servidor sozinho (aba 'MCP for Blender' no painel N)."
}
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
Write-Host "Pronto! No Blender, aperte N no viewport 3D > aba 'MCP for Blender' e confira se o servidor está rodando." -ForegroundColor Green
Write-Host "Depois volte ao Claude e peça para usar o Blender MCP."
if (-not $SemPerguntas) { Read-Host "Enter para fechar" | Out-Null }
