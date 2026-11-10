#!/usr/bin/env bash
# Сборка готового exe строго в publish/win-x64-selfcontained
# (единственный «раздаточный» путь — см. ../.clinerules и AGENTS.md).
set -euo pipefail

cd "$(dirname "$0")"

dotnet publish src/Zayavka.WinForms -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true \
  -o publish/win-x64-selfcontained

# Убираем промежуточные сборки, чтобы не путать с готовым exe.
rm -rf src/*/bin/Release

echo
echo "Готово: $(pwd)/publish/win-x64-selfcontained/Zayavka.WinForms.exe"
