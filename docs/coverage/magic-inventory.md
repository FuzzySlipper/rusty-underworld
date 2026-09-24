# Magic inventory

Status: inventory baseline, not tasks. Every spell name, formula, behavior,
skill description, and rune gloss below was transcribed verbatim from the
printed Player's Guide (`Ultima_Underworld-Manual.pdf` pp. 28–30, 32, extracted
with `pdftotext -layout`; printed page numbers cited). Nothing is reconstructed
or inferred. Donor dispatch knowledge is cited to
[`underworldgodot-survey.md`](../research/underworldgodot-survey.md) (§N).

## 1. Grimoire — 8 circles × 5 spells

### 1st Circle

| Spell | Formula | Behavior |
| --- | --- | --- |
| Create Food | In Mani Ylem | Causes a fine bounty of food to appear (permanent spell). |
| Light | In Lor | Illuminates a darkened area (duration spell). |
| Magic Arrow | Ort Jux | Fires a magic arrow at your opponent (targeted spell). |
| Resist Blows | Bet In Sanct | Has the same effect as wearing a suit of head-to-toe armor (duration spell). |
| Stealth | Sanct Hur | Briefly prevents you from making any noise, making it less likely that creatures will notice you (duration spell). |

### 2nd Circle

| Spell | Formula | Behavior |
| --- | --- | --- |
| Cause Fear | Quas Corp | May cause an opponent to lose heart and flee (instantaneous spell). |
| Detect Monster | Wis Mani | Reveals the presence of hidden or unperceived enemies (instantaneous spell). |
| Lesser Heal | In Bet Mani | Heals your minor wounds (instantaneous spell). |
| Rune of Warding | In Jux | Places an enchantment in an area which will report if anything disturbs it (permanent spell, until disturbed). |
| Slow Fall | Rel Des Por | Briefly allows you to float in the air like a feather (duration spell). |

### 3rd Circle

| Spell | Formula | Behavior |
| --- | --- | --- |
| Conceal | Bet Sanct Lor | Briefly obscures you, so you might remain unseen (duration spell). |
| Lightning | Ort Grav | Hurls a bolt of arcane energy at your opponent (targeted spell). |
| Night Vision | Quas Lor | Allows you to see without benefit of torch or candle (duration spell). |
| Speed | Rel Tym Por | Slows down your enemies relative to your speed (duration spell). |
| Strengthen Door | Sanct Jux | Spikes a door (permanent spell). |

### 4th Circle

| Spell | Formula | Behavior |
| --- | --- | --- |
| Heal | In Mani | Heals you of grievous wounds (permanent spell). |
| Levitate | Hur Por | Briefly allows you to rise vertically into the air (duration spell). |
| Poison | Nox Mani | Poisons your opponent with toxic venom (permanent spell). |
| Remove Trap | An Jux | Negates the targeted snare (targeted spell). |
| Resist Fire | Sanct Flam | Briefly grants a partial resistance to damage from flame (duration spell). |

### 5th Circle

| Spell | Formula | Behavior |
| --- | --- | --- |
| Cure Poison | An Nox | Acts as an antidote to any poison (permanent spell). |
| Fireball | Por Flam | Hurls a mighty flaming missile at your opponent (targeted spell). |
| Missile Protection | Grav Sanct Por | Renders you invulnerable to missiles (duration spell). |
| Name Enchantment | Ort Wis Ylem | Reveals the true nature of the object on which you cast the spell (permanent spell). |
| Open | Ex Ylem | Unlocks a locked door or chest (permanent spell). |

### 6th Circle

| Spell | Formula | Behavior |
| --- | --- | --- |
| Daylight | Vas In Lor | Provides bright illumination for extended periods of time (duration spell). |
| Gate Travel | Vas Rel Por | Allows you to travel instantly to a moonstone (instantaneous spell). |
| Greater Heal | Vas In Mani | Brings you back to your original vigor (full Vitality) (permanent spell). |
| Paralyze | An Ex Por | Prevents target from moving (instantaneous spell). |
| Telekinesis | Ort Por Ylem | Allows you to pick up a single item and use it from a distance (duration spell). |

### 7th Circle

| Spell | Formula | Behavior |
| --- | --- | --- |
| Ally | In Mani Rel | Causes the ensorcelled being to fight the last enemy he or she saw you attack (permanent spell). |
| Confusion | Vas An Wis | Causes foes to act as if drunk (instantaneous spell). |
| Fly | Vas Hur Por | Allows you to fly through the air for a time, and then glide gently to the ground (duration spell). |
| Invisibility | Vas Sanct Lor | Causes you to become nearly impossible to see (duration spell). |
| Reveal | Ort An Quas | Reveals hidden objects and concealed exits from current location (instantaneous spell). |

### 8th Circle

| Spell | Formula | Behavior |
| --- | --- | --- |
| Flame Wind | Flam Hur | Casts multiple flaming missiles into the area (instantaneous spell). |
| Freeze Time | An Tym | Stops the flow of time for all but you (duration spell). |
| Iron Flesh | In Vas Sanct | Greatly increases your resistance to damage (duration spell). |
| Roaming Sight | Ort Por Wis | Allows you to see the world from a bird's-eye view (duration spell). |
| Tremor | Vas Por Ylem | Causes the ground to quake and rocks to burst (instantaneous spell). |

Grimoire-adjacent manual facts: Night Vision / Daylight / Light substitute for
torches and candles (pp. 6, 10–11); Create Food answers hunger (p. 19); flying
via spell or item uses `E` ascend / `Q` descend-land (p. 21); area spells can
destroy nearby valuables (Flame Wind warning, p. 27).

## 2. The 24 runes (p. 32)

| Rune | Gloss | Rune | Gloss | Rune | Gloss | Rune | Gloss |
| --- | --- | --- | --- | --- | --- | --- | --- |
| AN | Negate | IN | Cause | QUAS | Illusion | UUS | Raise |
| BET | Small | JUX | Harm | REL | Change | VAS | Great |
| CORP | Death | KAL | Summon | SANCT | Protection | WIS | Knowledge |
| DES | Down | LOR | Light | TYM | Time | YLEM | Matter |
| EX | Freedom | MANI | Life | — | — | — | — |
| FLAM | Flame | NOX | Poison | — | — | — | — |
| GRAV | Energy | ORT | Magic | — | — | — | — |
| HUR | Wind | POR | Movement | — | — | — | — |

Rune-bag facts (pp. 10–11, 21, 27): stones never leave the rune bag once in;
the bag opens an alphabetical panel where blanks mean missing; the Rune Shelf
persists so spells can be pre-buffed; the bottom symbol clears the shelf.

## 3. Casting gates (p. 26 unless noted)

- Who may attempt: anyone with runes + bag; success scales with Casting skill,
  Mana, and character level (pp. 11, 26).
- Mana cost: 3 × Circle.
- Level gate: character level/2 rounded up must meet the Circle.
- Recast delay scales with level + Circle (exact table in the printed guide's
  magic chapter; not transcribed here).
- Failures cost no Mana (only time) except rare backfires that wound
  low-Casting casters (pp. 11, 26).
- Targeted casting via RIGHT-click on the shelf: red circle = combat, blue
  cross = utility; needs clear space ahead; blocked point-blank fails (p. 27).
- Durations: icons left of the compass; max 3 concurrent; LEFT-click dispels,
  RIGHT-click names + stable/unstable time. Spells fade, e.g. over sleep
  (pp. 19, 27).
- Enchanted items bypass level/Mana: wearables act while worn, scrolls act on
  Use; all look mundane until Lore identifies them on Look (p. 27).
- Level-ups to max 16 raise Vitality + unlock Circles; skill-ups are MANUAL at
  ankh shrines (p. 20).

## 4. Skill list with governing attributes (p. 30)

Acrobat (DX) — move with grace; reduces fall/collision damage. Appraise (DX) —
perceive goods' value; aids barter evaluation. Attack (ST) — general fighting;
bonus to hit. Axe (ST) — axes; defense + bonus to hit with axes. Casting
(INT) — improves cast success. Charm (DX) — making friends; better barter
deals. Defense (ST) — penalty to foes trying to strike. Lore (INT) — identify
items; accuracy of Look information. Mace (ST) — blunt weapons; defense +
bonus to hit with mace/cudgel. Mana (INT) — increases maximum Mana. Missile
(ST) — bows/crossbows/slings; increases missile damage. Picklock (DX) —
lockpick use on doors/chests. Repair (DX) — anvil repair success. Search
(DX) — detect hidden doors and traps; applied automatically on Look. Sneak
(DX) — move quietly automatically. Swimming (DX) — postpones drowning. Sword
(ST) — swords/daggers; defense + bonus to hit. Track (DX) — perceive animal
tracks; tells when creatures are near. Traps (DX) — disarm found traps.
Unarmed (ST) — bonus to hit and damage with fists.

## 5. Godot spell-dispatch mapping (survey §6)

Format/dispatch knowledge only; nothing ported. `src/magic/spellcasting.cs`
(`CastSpell(major, minor, caster, target, tileX, tileY, CastOnEquip)`) fans out
by major and deducts `RunicMagic.PendingSpellCost` from mana;
`src/magic/runicmagic.cs` is the rune-sequence → (major, minor) table;
`src/magic/spellcasting_activeeffects.cs` is the stable/unstable duration
hook; `src/magic/spellcasting_objects.cs` gates enchanted-equipment casting
("not all spells will cast from here" — exact allowlist unverified).

| Major | Role | Donor file |
| --- | --- | --- |
| 0–3 | Motion/misc | `spellcasting_class_0123.cs` |
| 4 | Heal (minor-0xF special-cased) | `spellcasting_class_4.cs` |
| 5 | Projectile (separate UW1 vs UW2 ID tables) | `spellcasting_class_5.cs` |
| 6 | Area-around-player | `spellcasting_class_6.cs` |
| 7 | Callback / click-on-object (targeted) | `spellcasting_class_7.cs` |
| 8 | Summoning (UW1-vs-UW2 minor-4 branch) | `spellcasting_class_8.cs` |
| 9 | Curse (with on-equip path) | `spellcasting_class_9.cs` |
| 10 | Mana boost | `spellcasting_class_10.cs` |
| 11 | Misc/special (speed, portal) | `spellcasting_class_11.cs` |
| 12 | "Not castable here" | `spellcasting_class_12.cs` |
| 13 | Misc (Altaras wand, mind blast, …) | `spellcasting_class_13.cs` |
| 14 | Cutscene | (file span per survey) |

UW2-only branches (donor context, never extraction sources): the UW2
projectile table (major 5), the UW2 summon branch (major 8), UW2
enchantment items 224–231 (`MagicEnchantment.cs`; UW1 items 187–188 apply),
UW2 timer triggers (`src/World/timers.cs`) and the `src/scd/` engine.

## 6. Family grouping notes (inventory lens, not tasks)

- Damage (Magic Arrow, Lightning, Fireball, Flame Wind, Tremor, Poison):
  majors 5 + 6; Kit targeting/area machinery + ruleset damage policy.
- Light (Light, Night Vision, Daylight): Kit light/condition + duration;
  brightness/radius in tuning.
- Healing (Lesser Heal, Heal, Greater Heal, Cure Poison): Kit
  condition/recovery + ruleset amounts.
- Protection (Resist Blows, Resist Fire, Missile Protection, Iron Flesh,
  Rune of Warding, Strengthen Door): Kit resistance/condition + ruleset
  mitigation; warding/door-spiking as world-state effects.
- Movement (Slow Fall, Levitate, Fly, Speed, Gate Travel, Freeze Time,
  Telekinesis): Kit movement/physics/session-stepping + ruleset costs.
- Detection (Detect Monster, Reveal, Name Enchantment): likely ruleset policy
  over Kit targeting/notification values; verify against per-spell minors.
- Utility (Create Food, Open, Cause Fear, Ally, Confusion, Charm-adjacent,
  Roaming Sight, Paralyze): mixed — object creation, attitude changes, and
  remote sensing across Kit container/AI/presentation owners.
- Enchanted-item/potion overlay: cross-cuts all families; Kit
  casting-workflow + condition machinery with ruleset item-effect tables;
  Lore gate stays ruleset policy.

## 7. Remaining gaps

Numeric success formula and backfire rate; recast-delay table; per-spell
duration/stability classes; per-spell (major, minor) minors beyond the survey
shape; enchanted-equipment allowlist; potion brewing vs fixed effects;
targeting correspondence (red/blue cursors vs raycast/position entry points);
Freeze Time / Roaming Sight engine hooks (UW2 timer paths stay out of scope).
