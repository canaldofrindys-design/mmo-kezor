#!/usr/bin/env bash
# Instala ou atualiza o servidor Kezor num VPS Linux (Ubuntu/Debian), como root.
# Uso: bash instalar-servidor.sh <pasta com o Kezor.Server publicado>
# Variáveis opcionais na primeira instalação: KEZOR_GM_ACCOUNTS="conta1,conta2" KEZOR_NAME="Kezor" KEZOR_PORT=55901
# Os saves ficam em /opt/kezor/data e nunca são apagados por este script. Backup diário em /opt/kezor/backups (14 dias).
set -euo pipefail

SRC="${1:-$(dirname "$0")}"
if [ "$(id -u)" -ne 0 ]; then echo "Rode como root (ou com sudo)."; exit 1; fi
if [ ! -f "$SRC/Kezor.Server" ]; then echo "Não achei $SRC/Kezor.Server"; exit 1; fi

echo "==> Usuário e pastas"
id kezor >/dev/null 2>&1 || useradd --system --home /opt/kezor --shell /usr/sbin/nologin kezor
mkdir -p /opt/kezor/bin /opt/kezor/data /opt/kezor/backups /etc/kezor

echo "==> Parando o servidor (se estiver rodando; ele salva todo mundo antes)"
systemctl stop kezor-server 2>/dev/null || true

echo "==> Backup dos saves antes de atualizar"
if [ -n "$(ls -A /opt/kezor/data 2>/dev/null)" ]; then
  tar -czf "/opt/kezor/backups/antes-de-atualizar-$(date +%Y%m%d-%H%M%S).tar.gz" -C /opt/kezor data
fi

echo "==> Copiando o servidor"
find /opt/kezor/bin -mindepth 1 -delete
cp -a "$SRC"/. /opt/kezor/bin/
rm -f /opt/kezor/bin/instalar-servidor.sh /opt/kezor/bin/kezor-server.service
chmod +x /opt/kezor/bin/Kezor.Server

if [ ! -f /etc/kezor/kezor.env ]; then
  echo "==> Configuração inicial (/etc/kezor/kezor.env)"
  cat > /etc/kezor/kezor.env <<CONF
# Configuração do servidor Kezor. Depois de mudar: systemctl restart kezor-server
KEZOR_PORT=${KEZOR_PORT:-55901}
KEZOR_NAME=${KEZOR_NAME:-Kezor}
# contas com comandos GM, separadas por vírgula
KEZOR_GM_ACCOUNTS=${KEZOR_GM_ACCOUNTS:-}
CONF
fi
chown -R kezor:kezor /opt/kezor
chmod 640 /etc/kezor/kezor.env; chgrp kezor /etc/kezor/kezor.env

echo "==> Serviço (liga sozinho e reinicia se cair)"
cp "$(dirname "$0")/kezor-server.service" /etc/systemd/system/kezor-server.service 2>/dev/null || cp "$SRC/kezor-server.service" /etc/systemd/system/kezor-server.service
systemctl daemon-reload
systemctl enable kezor-server >/dev/null
systemctl restart kezor-server

echo "==> Backup diário dos saves (03:30, guarda 14 dias)"
cat > /etc/cron.d/kezor-backup <<'CRON'
30 3 * * * root tar -czf /opt/kezor/backups/saves-$(date +\%Y\%m\%d).tar.gz -C /opt/kezor data && find /opt/kezor/backups -name '*.tar.gz' -mtime +14 -delete
CRON

PORT=$(grep -E '^KEZOR_PORT=' /etc/kezor/kezor.env | cut -d= -f2); PORT=${PORT:-55901}
echo "==> Firewall: liberando a porta $PORT"
if command -v ufw >/dev/null 2>&1; then
  ufw allow OpenSSH >/dev/null 2>&1 || true
  ufw allow "$PORT"/tcp >/dev/null 2>&1 || true
fi

sleep 2
if systemctl is-active --quiet kezor-server; then
  echo ""
  echo "PRONTO! Servidor Kezor ligado na porta $PORT."
  echo "Ver o que está acontecendo:   journalctl -u kezor-server -f"
  echo "Reiniciar / parar:            systemctl restart kezor-server  /  systemctl stop kezor-server"
else
  echo "O servidor não ligou. Veja o erro com: journalctl -u kezor-server -n 50"
  exit 1
fi
