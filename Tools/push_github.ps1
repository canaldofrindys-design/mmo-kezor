# Kezor - envia o projeto para o GitHub automaticamente.
# Uso:  powershell -ExecutionPolicy Bypass -File Tools\push_github.ps1 [-Mensagem "texto do commit"]
param(
    [string]$Mensagem = "",
    [string]$Remote = "https://github.com/canaldofrindys-design/mmo-kezor.git",
    [string]$Branch = "main"
)
$ErrorActionPreference = "Continue"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

# 1) .gitignore / .gitattributes (cria so se nao existirem)
if (-not (Test-Path ".gitignore")) {
@'
# Unity
/[Ll]ibrary/
/[Tt]emp/
/[Oo]bj/
/[Bb]uild*/bin/
/[Bb]uild*/obj/
/[Bb]uilds/
/[Ll]ogs/
/[Uu]ser[Ss]ettings/
/[Mm]emory[Cc]aptures/
/*.csproj
/*.sln
/*.slnx
.vs/
.vscode/
*.pidb.meta
*.pdb.meta
*.mdb.meta
sysinfo.txt
crashlytics-buildid.txt
# Segredos
.env
.env.*
*.pem
*.key
*credentials*.json
# Arquivos grandes (gerado pelo script)
'@ | Out-File -Encoding utf8 ".gitignore"
}
if (-not (Test-Path ".gitattributes")) { "* text=auto" | Out-File -Encoding utf8 ".gitattributes" }

# 2) Arquivos > 100 MB vao para o .gitignore; lista > 50 MB
$skip = @('\Library\','\Temp\','\Obj\','\Logs\','\UserSettings\','\MemoryCaptures\','\.git\','\.vs\')
$grandes = Get-ChildItem -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
    $p = $_.FullName; -not ($skip | Where-Object { $p -like "*$_*" }) -and $_.Length -gt 50MB
}
$excluidos = @()
foreach ($f in $grandes) {
    $rel = $f.FullName.Substring($root.Length + 1).Replace('\','/')
    if ($f.Length -gt 100MB) {
        $excluidos += $rel
        if (-not (Select-String -Path ".gitignore" -SimpleMatch "/$rel" -Quiet)) { Add-Content ".gitignore" "/$rel" }
    } else { Write-Host ("Aviso >50MB: {0} ({1:N0} MB)" -f $rel, ($f.Length/1MB)) }
}

# 3) Repositorio
if (-not (Test-Path ".git")) { git init -b $Branch | Out-Null }
if (-not (git remote | Select-String -SimpleMatch "origin" -Quiet)) { git remote add origin $Remote }
git add -A
git diff --cached --quiet
if ($LASTEXITCODE -ne 0) {
    if (-not $Mensagem) { $Mensagem = "Kezor: atualizacao " + (Get-Date -Format "yyyy-MM-dd HH:mm") }
    git commit -q -m $Mensagem -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
} else { Write-Host "Nada novo para commitar." }

# 4) Push com ate 4 tentativas
$ok = $false
for ($i = 1; $i -le 5 -and -not $ok; $i++) {
    git push -u origin $Branch 2>&1 | Select-Object -Last 3
    if ($LASTEXITCODE -eq 0) { $ok = $true } elseif ($i -lt 5) { Start-Sleep -Seconds ([math]::Pow(2, $i)) }
}

$tam = (git count-objects -vH | Select-String "size-pack").ToString()
Write-Host "Push ok: $(if ($ok) {'sim'} else {'nao'}) | $tam"
if ($excluidos) { Write-Host "Excluidos (>100MB): $($excluidos -join ', ')" } else { Write-Host "Excluidos (>100MB): nenhum" }
