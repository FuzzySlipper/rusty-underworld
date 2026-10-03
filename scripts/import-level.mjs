// Operator step: import one level of the emulated game from operator-supplied
// data into the product content tree.
//
// Original game data is never committed, so the generated packs live under
// content/abyss/imports/ (git-ignored). The default bundle admits every pack under
// that root without naming it, so importing never edits a tracked file: level 1
// alone is enough to launch, and each further level becomes reachable by travel
// once it is imported. Launching with no level imported fails with this command.
//
//   node scripts/import-level.mjs [level] [data-dir]
//
// Defaults: level 1 from local/extracted/uw/UW/DATA (UW1 tree only; the UW2
// tree inside the ISO is never an extraction source). Runs the same under
// Windows and Linux.
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync } from 'node:fs';
import { join, resolve } from 'node:path';

const repoRoot = resolve(import.meta.dirname, '..');
const level = process.argv[2] ?? '1';
const dataDir = resolve(process.argv[3] ?? join(repoRoot, 'local', 'extracted', 'uw', 'UW', 'DATA'));
const data = (name) => join(dataDir, name);

if (!existsSync(data('LEV.ARK')) || !existsSync(data('TERRAIN.DAT'))) {
  console.error(`Operator UW1 data not found in ${dataDir} (need LEV.ARK and TERRAIN.DAT).`);
  process.exit(1);
}

const out = join(repoRoot, 'content', 'abyss', 'imports', `level-${level}`);
// The object tables are generated from the operator's own OBJECTS.DAT, so they
// live in the ignored imports tree beside the level, not with the authored packs.
const packs = join(repoRoot, 'content', 'abyss', 'imports', 'object-tables');
mkdirSync(packs, { recursive: true });

// The object tables are install-global (critters, containers, the item catalog,
// strings and conversations), so they have one directory of their own; the
// level's placements ride with the level.
const tables = [];
if (existsSync(data('OBJECTS.DAT'))) {
  tables.push('--objects', data('OBJECTS.DAT'), '--packs', packs);
  // The item catalog needs the common object table and the name strings too.
  if (existsSync(data('COMOBJ.DAT'))) tables.push('--common', data('COMOBJ.DAT'));
  if (existsSync(data('STRINGS.PAK'))) tables.push('--strings', data('STRINGS.PAK'));
  // The conversations the game's own creatures hold, and the strings they read.
  if (existsSync(data('CNV.ARK'))) tables.push('--cnv', data('CNV.ARK'));
} else {
  console.error(`OBJECTS.DAT missing in ${dataDir}: the level imports without critter and container tables.`);
}

try {
  execFileSync('dotnet', [
    'run', '--project', join(repoRoot, 'src', 'UltimaUnderworld.Import.Tool', 'UltimaUnderworld.Import.Tool.csproj'),
    '--configuration', 'Release', '--',
    'emit-level', '--levark', data('LEV.ARK'), '--terrain', data('TERRAIN.DAT'), '--level', level, '--out', out,
    ...tables,
  ], { stdio: 'inherit' });
} catch (error) {
  process.exit(error.status ?? 1);
}
console.log(`Imported level ${level} into ${out} (placements included). Rebuild the product to stage it.`);
