# Envia o projeto Kezor (C:\MuProtoUnity) para o GitHub privado mmo-kezor.
# Uso: duplo clique em Enviar-GitHub.bat. Pode rodar de novo quando quiser
# para enviar as alteracoes mais recentes.

$ErrorActionPreference = 'Continue'
$Projeto = 'C:\MuProtoUnity'
$Remoto  = 'https://github.com/canaldofrindys-design/mmo-kezor.git'
$LimiteMB = 95   # GitHub recusa arquivos acima de 100 MB

function Passo($t) { Write-Host ""; Write-Host "==> $t" -ForegroundColor Cyan }
function Falha($t) { Write-Host ""; Write-Host "ERRO: $t" -ForegroundColor Red; Read-Host "Aperte Enter para fechar"; exit 1 }

# 1. Git instalado?
Passo "Verificando o Git"
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Host "Git nao encontrado. Instalando pelo winget (vai pedir confirmacao)..."
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        Falha "winget nao existe neste Windows. Instale o Git em https://git-scm.com/download/win e rode este script de novo."
    }
    winget install --id Git.Git -e --source winget --accept-package-agreements --accept-source-agreements
    $env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User')
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        Falha "O Git foi instalado, mas precisa abrir uma janela nova. Feche esta e rode o script de novo."
    }
}
git --version

if (-not (Test-Path $Projeto)) { Falha "Pasta $Projeto nao encontrada." }
Set-Location $Projeto

# 2. .gitignore e .gitattributes do Unity
Passo "Preparando .gitignore"
$ignore = @'
# Gerado pelo Unity (recriado sozinho ao abrir o projeto)
/[Ll]ibrary/
/[Tt]emp/
/[Oo]bj/
/[Ll]ogs/
/[Uu]ser[Ss]ettings/
/[Mm]emoryCaptures/
/[Rr]ecordings/
/[Bb]uilds/
/*.csproj
/*.sln
/*.slnx
*.pidb.meta
*.pdb.meta
*.mdb.meta
sysinfo.txt
crashlytics-build.properties

# Compilacao dos projetos auxiliares
Build/**/bin/
Build/**/obj/

# Editores
.vs/
.vscode/
.idea/
*.suo
*.user

# Segredos
.env
*.env
credentials*.json
'@
if (-not (Test-Path '.gitignore')) { Set-Content '.gitignore' $ignore -Encoding UTF8 }
if (-not (Test-Path '.gitattributes')) { Set-Content '.gitattributes' '* text=auto' -Encoding UTF8 }

# 3. Repositorio local
Passo "Preparando o repositorio"
if (-not (Test-Path '.git')) { git init | Out-Null }
git branch -M main 2>$null
if (-not (git config user.name))  { git config user.name  'Alex Lima' }
if (-not (git config user.email)) { git config user.email 'canaldofrindys-design@users.noreply.github.com' }
$temRemoto = (git remote) -contains 'origin'
if ($temRemoto) { git remote set-url origin $Remoto } else { git remote add origin $Remoto }

# 4. Arquivos grandes demais ficam de fora
Passo "Procurando arquivos acima de $LimiteMB MB"
$excluir = @('\Library\','\Temp\','\Obj\','\Logs\','\.git\','\bin\','\obj\')
$grandes = Get-ChildItem -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Length -gt ($LimiteMB * 1MB) } |
    Where-Object { $p = $_.FullName; -not ($excluir | Where-Object { $p -like "*$_*" }) }
if ($grandes) {
    Add-Content '.gitignore' "`n# Grandes demais para o GitHub"
    foreach ($g in $grandes) {
        $rel = $g.FullName.Substring($Projeto.Length + 1).Replace('\','/')
        Add-Content '.gitignore' "/$rel"
        Write-Host ("  fora do envio: {0} ({1:N0} MB)" -f $rel, ($g.Length/1MB)) -ForegroundColor Yellow
    }
} else { Write-Host "  nenhum" }

# 5. Commit
Passo "Criando o commit"
git add -A
git diff --cached --quiet
if ($LASTEXITCODE -ne 0) {
    git commit -q -m ("Kezor: atualizacao " + (Get-Date -Format 'yyyy-MM-dd HH:mm'))
} else { Write-Host "  nada novo para enviar" }

# 6. Junta com o que o Claude enviou pela nuvem antes de mandar
Passo "Baixando as alteracoes do Claude"
git pull --rebase --autostash origin main 2>$null
if ($LASTEXITCODE -ne 0) { git rebase --abort 2>$null; Falha "Conflito ao juntar com o GitHub. Tire um print desta janela e mande para o Claude." }

# 7. Envio (na primeira vez abre o navegador para entrar no GitHub)
Passo "Enviando para o GitHub (na primeira vez abre o login no navegador)"
git -c http.postBuffer=524288000 push -u origin main
if ($LASTEXITCODE -ne 0) { Falha "O envio falhou. Tire um print desta janela e mande para o Claude." }

Write-Host ""
Write-Host "PRONTO! Projeto enviado para github.com/canaldofrindys-design/mmo-kezor" -ForegroundColor Green
Write-Host "Avise o Claude: 'enviado'."
Read-Host "Aperte Enter para fechar"
