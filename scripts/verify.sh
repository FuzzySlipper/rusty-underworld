#!/usr/bin/env bash
# Routine repository verification for rusty-underworld.
#
# Proves the installed Engine pair and the product shape (`rusty status`),
# builds every product project, stages the Host (which compiles the DOM
# companion through the SDK's UI build), and then runs every suite: the DOM
# tests against the staged UI, and each .NET suite. Every stage runs even when
# an earlier suite fails, so one run reports everything that is broken; the
# script exits non-zero if any stage failed.
#
# The lists are explicit: a discovery-based loop silently stops covering a
# project whose csproj moved or was renamed.
#
# NativeAOT is a separate fidelity/release target and stays opt-in through
# --aot; the ordinary development loop does not need it.
set -uo pipefail

aot=false
for argument in "$@"; do
  case "$argument" in
    --aot) aot=true ;;
    -h|--help)
      echo "usage: scripts/verify.sh [--aot]"
      echo "  no arguments  pinned pair install and status, UI dependencies, product build, Host staging, every suite"
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

failures=()
run() {
  local label=$1
  shift
  if ! "$@"; then
    failures+=("$label")
    echo "FAILED: $label" >&2
  fi
}

# Installs the pinned pair when it is missing (a no-op offline once installed),
# then reports the pin, the pair and the project shape the CLI checks.
rusty install || { echo "rusty install failed; nothing else can build." >&2; exit 1; }
rusty status || { echo "rusty status reports the product is not ready." >&2; exit 1; }
pair_version=$(sed -n 's|.*<RustyEnginePackageVersion>\([^<]*\)</RustyEnginePackageVersion>.*|\1|p' Directory.Build.props)

# The product UI is a Node-built DOM companion; its compiler and DOM test
# dependencies come from the lockfile.
npm ci || { echo "npm ci failed; the UI cannot build." >&2; exit 1; }

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
# The DOM suite is not a .NET project; it runs against the UI staging compiles.
ui_tests=(tests/AbyssRpg.Ui.Tests/*.test.mjs)
host_project="src/AbyssRpg.Host/AbyssRpg.Host.csproj"

for project in "${product_projects[@]}"; do
  run "build $project" bash -c "dotnet restore '$project' && dotnet build '$project' --configuration Release --no-restore"
done

# Staging runs the SDK's UI build (scripts/build-ui.mjs against the pair's UI
# declarations), so the DOM tests below exercise the UI the product ships.
run "stage $host_project" dotnet msbuild "$host_project" -t:StageRustyEngineCoreClrProduct -p:Configuration=Release

run "DOM suite" node --test "${ui_tests[@]}"
for suite in "${test_projects[@]}"; do
  run "test $suite" dotnet test "$suite"
done

if [[ "$aot" == true ]]; then
  run "NativeAOT $host_project" dotnet msbuild "$host_project" -t:VerifyRustyEngineAot -p:Configuration=Release
fi

if [[ ${#failures[@]} -gt 0 ]]; then
  echo "Verification failed for Engine pair ${pair_version}:" >&2
  printf '  %s\n' "${failures[@]}" >&2
  exit 1
fi

if [[ "$aot" == true ]]; then
  echo "Verified Engine pair ${pair_version}: CoreCLR and NativeAOT."
else
  echo "Verified Engine pair ${pair_version}: CoreCLR. Use --aot for the NativeAOT fidelity publish."
fi
