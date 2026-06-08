#!/usr/bin/env bash
#
# Publica binários distribuíveis do Agente de Otimização de Hardware.
# Gera executáveis self-contained (não exigem .NET instalado na máquina alvo).
#
# Uso:
#   scripts/publish.sh                 # publica a CLI para linux-x64, win-x64, osx-x64
#   scripts/publish.sh linux-x64       # publica a CLI só para um RID
#   COM_UI=1 scripts/publish.sh        # também publica a UI Avalonia (desktop)
#
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SAIDA="${RAIZ}/artifacts"
CLI="${RAIZ}/src/HardwareOptimizer.Cli/HardwareOptimizer.Cli.csproj"
APP="${RAIZ}/src/HardwareOptimizer.App/HardwareOptimizer.App.csproj"

if [[ $# -gt 0 ]]; then
  RIDS=("$@")
else
  RIDS=("linux-x64" "win-x64" "osx-x64")
fi

publicar() {
  local proj="$1" rid="$2" nome="$3"
  echo ">> Publicando ${nome} para ${rid}"
  dotnet publish "${proj}" \
    -c Release -r "${rid}" --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "${SAIDA}/${nome}-${rid}"
}

for rid in "${RIDS[@]}"; do
  publicar "${CLI}" "${rid}" "hwopt-cli"
  if [[ "${COM_UI:-0}" == "1" ]]; then
    publicar "${APP}" "${rid}" "hwopt-ui"
  fi
done

echo ""
echo "Artefatos gerados em: ${SAIDA}/"
ls -1 "${SAIDA}" 2>/dev/null || true
