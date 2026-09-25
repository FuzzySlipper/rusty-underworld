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
# The object tables are generated from the operator's own OBJECTS.DAT, so they
# live in the ignored imports tree beside the level, not with the authored packs.
packs="$repo_root/content/abyss/imports/object-tables"
mkdir -p "$packs"

# The object tables are install-global (critters, containers), so they land
# beside the authored packs; the level's placements ride with the level.
if [[ -f "$data_dir/OBJECTS.DAT" ]]; then
  tables_args=(--objects "$data_dir/OBJECTS.DAT" --packs "$packs")
else
  tables_args=()
  echo "OBJECTS.DAT missing in $data_dir: the level imports without critter and container tables." >&2
fi

dotnet run --project "$repo_root/src/UltimaUnderworld.Import.Tool/UltimaUnderworld.Import.Tool.csproj" \
  --configuration Release -- \
  emit-level --levark "$data_dir/LEV.ARK" --terrain "$data_dir/TERRAIN.DAT" --level "$level" --out "$out" \
  "${tables_args[@]}"

echo "Imported level $level into $out (placements included). Rebuild the product to stage it."
