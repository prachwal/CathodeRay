#!/usr/bin/env bash
# Usage: tools/publish.sh [rid...]   (domyślnie linux-x64 win-x64)
# Buduje samodzielne pliki wykonywalne `cathode` do dist/<rid>/ oraz paczkę dotnet tool do dist/tool/.
# Wszystko powstaje w katalogu tymczasowym i trafia do dist/ dopiero po udanym zakończeniu wszystkich kroków.
# Instalacja: sudo install dist/linux-x64/cathode /usr/local/bin/   albo   dotnet tool install -g cathode --add-source dist/tool
set -euo pipefail
cd "$(dirname "$0")/.."
rids=("$@")
[ ${#rids[@]} -gt 0 ] || rids=(linux-x64 win-x64)
stage=$(mktemp -d)
trap 'rm -rf "$stage"' EXIT
for rid in "${rids[@]}"; do
  dotnet publish src/CathodeRay.Cli -c Release -r "$rid" --self-contained -p:PublishSingleFile=true -o "$stage/$rid"
  rm -f "$stage/$rid"/*.pdb "$stage/$rid"/*.xml
done
dotnet pack src/CathodeRay.Cli -c Release -o "$stage/tool"
mkdir -p dist
for entry in "$stage"/*; do
  rm -rf "dist/$(basename "$entry")"
  mv "$entry" dist/
done
