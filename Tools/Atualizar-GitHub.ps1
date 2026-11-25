# Baixa para C:\MuProtoUnity o que o Claude fez no GitHub (mmo-kezor).
# Uso: feche o Play do Unity, de duplo clique em Atualizar-GitHub.bat e depois volte ao Unity.

$ErrorActionPreference = 'Continue'
$Projeto = 'C:\MuProtoUnity'

function Falha($t) { Write-Host ""; Write-Host "ERRO: $t" -ForegroundColor Red; Read-Host "Aperte Enter para fechar"; exit 1 }

if (-not (Get-Command git -ErrorAction SilentlyContinue)) { Falha "Git nao encontrado. Rode antes o Enviar-GitHub.bat (ele instala o Git)." }
if (-not (Test-Path (Join-Path $Projeto '.git'))) { Falha "C:\MuProtoUnity ainda nao esta ligado ao GitHub. Rode antes o Enviar-GitHub.bat." }
Set-Location $Projeto

Write-Host "==> Baixando as alteracoes do Claude" -ForegroundColor Cyan
git pull --rebase --autostash origin main
if ($LASTEXITCODE -ne 0) { git rebase --abort 2>$null; Falha "Conflito ao atualizar. Tire um print desta janela e mande para o Claude." }

Write-Host ""
git log -3 --pretty=format:"  %h  %s" ; Write-Host ""
Write-Host ""
Write-Host "PRONTO! Projeto atualizado. Volte ao Unity, espere ele recarregar e aperte Play." -ForegroundColor Green
Read-Host "Aperte Enter para fechar"
