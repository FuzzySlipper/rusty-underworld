#!/usr/bin/env bash
# Routine repository verification for rusty-underworld.
#
# This pass landed the repository shell and the Kit bootstrap, so the script
# currently proves the installed Engine pair identity and the product UI
# toolchain, builds the checked-in projects, and runs the architecture suite.
# Add each landed project to `product_projects` and each suite to
# `test_projects`; the explicit lists are deliberate, because a
# discovery-based loop silently stops covering a project whose csproj moved
# or was renamed.
#
# NativeAOT is a separate fidelity/release target and stays opt-in through
# --aot; the ordinary development loop does not need it.
set -euo pipefail

aot=false
for argument in "$@"; do
  case "$argument" in
    --aot) aot=true ;;
    -h|--help)
      echo "usage: scripts/verify.sh [--aot]"
      echo "  no arguments  pinned pair install, UI dependencies, product build, suites, and host staging (once the Host entry lands)"
      echo "  --aot         also run the NativeAOT fidelity publish"
      exit 0
      ;;
    *)
      echo "Unknown argument: $argument (supported: --aot)" >&2
      exit 2
      ;;
  esac
done

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
cd "$repo_root"

# Installs the pinned pair when it is missing (a no-op offline once installed).
rusty install
pair_version=$(sed -n 's|.*<RustyEnginePackageVersion>\([^<]*\)</RustyEnginePackageVersion>.*|\1|p' Directory.Build.props)

# The product UI is a Node-built DOM companion. Its dependencies and its DOM
# tests are checked here alongside the product projects, because the UI
# toolchain is what the product companion work consumes.
npm ci
npx tsc -p src/ui
if compgen -G "tests/AbyssRpg.Ui.Tests/*.test.mjs" > /dev/null; then
  node --test tests/AbyssRpg.Ui.Tests/*.test.mjs
else
  echo "No product UI tests are checked in yet."
fi

# Every project this repository builds and runs. Explicit lists: a
# discovery-based loop silently stops covering a project that moved.
product_projects=(
  "src/AbyssRpg.Kit/AbyssRpg.Kit.csproj"
  "src/AbyssRpg.Rulesets.UltimaUnderworld/AbyssRpg.Rulesets.UltimaUnderworld.csproj"
  "src/AbyssRpg.Host/AbyssRpg.Host.csproj"
  "src/UltimaUnderworld.Import/UltimaUnderworld.Import.csproj"
  "src/UltimaUnderworld.Import.Tool/UltimaUnderworld.Import.Tool.csproj"
)
test_projects=(
  "tests/AbyssRpg.Architecture.Tests/AbyssRpg.Architecture.Tests.csproj"
  "tests/AbyssRpg.Kit.Tests/AbyssRpg.Kit.Tests.csproj"
  "tests/AbyssRpg.Rulesets.UltimaUnderworld.Tests/AbyssRpg.Rulesets.UltimaUnderworld.Tests.csproj"
  "tests/AbyssRpg.Host.Tests/AbyssRpg.Host.Tests.csproj"
  "tests/UltimaUnderworld.Import.Tests/UltimaUnderworld.Import.Tests.csproj"
)
host_project="src/AbyssRpg.Host/AbyssRpg.Host.csproj"
# Set once the Host entry became a stageable product composition
# (IEngineProduct + SDK targets); staging and --aot run against it.

if [[ ${#product_projects[@]} -eq 0 ]]; then
  echo "No product projects are listed in product_projects."
  echo "Verified Engine pair ${pair_version} with UI dependencies installed."
  [[ "$aot" == false ]] || {
    echo "NativeAOT verification needs a stageable product host project (none listed yet)." >&2
    exit 1
  }
  exit 0
fi

for project in "${product_projects[@]}"; do
  dotnet restore "$project"
  dotnet build "$project" --configuration Release --no-restore
done
for suite in "${test_projects[@]}"; do
  dotnet test "$suite"
done

# Compiling projects is not the same as exercising them, and a green build says
# nothing about a suite nobody ran, so every checked suite above is executed.

[[ -z "$host_project" ]] || dotnet msbuild "$host_project" -t:StageRustyEngineCoreClrProduct -p:Configuration=Release

if [[ "$aot" == true ]]; then
  [[ -n "$host_project" ]] || {
    echo "NativeAOT verification needs a stageable product host project (Host shell has no entry yet; see UW-T01)." >&2
    exit 1
  }
  dotnet msbuild "$host_project" -t:VerifyRustyEngineAot -p:Configuration=Release
  echo "Verified Engine pair ${pair_version}: CoreCLR and NativeAOT."
else
  echo "Verified Engine pair ${pair_version}: CoreCLR. Use --aot for the NativeAOT fidelity publish."
fi
