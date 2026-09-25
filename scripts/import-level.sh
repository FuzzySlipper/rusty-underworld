#!/usr/bin/env bash
# Operator step: import one level of the emulated game from operator-supplied
# data into the product content tree.
#
# Original game data is never committed, so this generated level pack lives
# under content/abyss/imports/ (git-ignored). The product's default bundle
# selects it by id; launching without it fails with the command to run.
#
#   scripts/import-level.sh [level] [data-dir]
#
# Defaults: level 1 from local/extracted/uw/UW/DATA (UW1 tree only; the UW2
# tree inside the ISO is never an extraction source).
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
level=${1:-1}
data_dir=${2:-$repo_root/local/extracted/uw/UW/DATA}

if [[ ! -f "$data_dir/LEV.ARK" || ! -f "$data_dir/TERRAIN.DAT" ]]; then
  echo "Operator UW1 data not found in $data_dir (need LEV.ARK and TERRAIN.DAT)." >&2
  exit 1
fi

out="$repo_root/content/abyss/imports/level-$level"
dotnet run --project "$repo_root/src/UltimaUnderworld.Import.Tool/UltimaUnderworld.Import.Tool.csproj" \
  --configuration Release -- \
  emit-level --levark "$data_dir/LEV.ARK" --terrain "$data_dir/TERRAIN.DAT" --level "$level" --out "$out"
echo "Imported level $level into $out. Rebuild the product to stage it."
