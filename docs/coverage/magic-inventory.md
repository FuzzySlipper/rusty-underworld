# Magic inventory

A factual inventory; reference material, not a statement of what this
repository implements. Spell names, formulas, and behavior
statements were transcribed verbatim from the printed Player's Guide
(`Ultima_Underworld-Manual.pdf` pp. 28–29, extracted with `pdftotext -layout`;
printed page numbers cited). Skill entries (§4) are condensed from the p. 30
list — substance checked, wording mine. Rune glosses (§2) are verbatim from
p. 32. Nothing is reconstructed or inferred. Donor dispatch knowledge is cited to
[`underworldgodot-survey.md`](../research/underworldgodot-survey.md) (§N).

## 1. Grimoire — 8 circles × 5 spells

### 1st Circle

| ID | Spell | Formula | Behavior |
| --- | --- | --- | --- |
| M-01 Create Food | In Mani Ylem | Causes a fine bounty of food to appear (permanent spell). |
| M-02 Light | In Lor | Illuminates a darkened area (duration spell). |
| M-03 Magic Arrow | Ort Jux | Fires a magic arrow at your opponent (targeted spell). |
| M-04 Resist Blows | Bet In Sanct | Has the same effect as wearing a suit of head-to-toe armor (duration spell). |
| M-05 Stealth | Sanct Hur | Briefly prevents you from making any noise, making it less likely that creatures will notice you (duration spell). |

### 2nd Circle

| ID | Spell | Formula | Behavior |
| --- | --- | --- | --- |
| M-06 Cause Fear | Quas Corp | May cause an opponent to lose heart and flee (instantaneous spell). |
| M-07 Detect Monster | Wis Mani | Reveals the presence of hidden or unperceived enemies (instantaneous spell). |
| M-08 Lesser Heal | In Bet Mani | Heals your minor wounds (instantaneous spell). |
| M-09 Rune of Warding | In Jux | Places an enchantment in an area which will report if anything disturbs it (permanent spell, until disturbed). |
| M-10 Slow Fall | Rel Des Por | Briefly allows you to float in the air like a feather (duration spell). |

### 3rd Circle

| ID | Spell | Formula | Behavior |
| --- | --- | --- | --- |
| M-11 Conceal | Bet Sanct Lor | Briefly obscures you, so you might remain unseen (duration spell). |
| M-12 Lightning | Ort Grav | Hurls a bolt of arcane energy at your opponent (targeted spell). |
| M-13 Night Vision | Quas Lor | Allows you to see without benefit of torch or candle (duration spell). |
| M-14 Speed | Rel Tym Por | Slows down your enemies relative to your speed (duration spell). |
| M-15 Strengthen Door | Sanct Jux | Spikes a door (permanent spell). |

### 4th Circle

| ID | Spell | Formula | Behavior |
| --- | --- | --- | --- |
| M-16 Heal | In Mani | Heals you of grievous wounds (permanent spell). |
| M-17 Levitate | Hur Por | Briefly allows you to rise vertically into the air (duration spell). |
| M-18 Poison | Nox Mani | Poisons your opponent with toxic venom (permanent spell). |
| M-19 Remove Trap | An Jux | Negates the targeted snare (targeted spell). |
| M-20 Resist Fire | Sanct Flam | Briefly grants a partial resistance to damage from flame (duration spell). |

### 5th Circle

| ID | Spell | Formula | Behavior |
| --- | --- | --- | --- |
| M-21 Cure Poison | An Nox | Acts as an antidote to any poison (permanent spell). |
| M-22 Fireball | Por Flam | Hurls a mighty flaming missile at your opponent (targeted spell). |
| M-23 Missile Protection | Grav Sanct Por | Renders you invulnerable to missiles (duration spell). |
| M-24 Name Enchantment | Ort Wis Ylem | Reveals the true nature of the object on which you cast the spell (permanent spell). |
| M-25 Open | Ex Ylem | Unlocks a locked door or chest (permanent spell). |

### 6th Circle

| ID | Spell | Formula | Behavior |
| --- | --- | --- | --- |
| M-26 Daylight | Vas In Lor | Provides bright illumination for extended periods of time (duration spell). |
| M-27 Gate Travel | Vas Rel Por | Allows you to travel instantly to a moonstone (instantaneous spell). |
| M-28 Greater Heal | Vas In Mani | Brings you back to your original vigor (full Vitality) (permanent spell). |
| M-29 Paralyze | An Ex Por | Prevents target from moving (instantaneous spell). |
| M-30 Telekinesis | Ort Por Ylem | Allows you to pick up a single item and use it from a distance (duration spell). |

### 7th Circle

| ID | Spell | Formula | Behavior |
| --- | --- | --- | --- |
| M-31 Ally | In Mani Rel | Causes the ensorcelled being to fight the last enemy he or she saw you attack (permanent spell). |
| M-32 Confusion | Vas An Wis | Causes foes to act as if drunk (instantaneous spell). |
| M-33 Fly | Vas Hur Por | Allows you to fly through the air for a time, and then glide gently to the ground (duration spell). |
| M-34 Invisibility | Vas Sanct Lor | Causes you to become nearly impossible to see (duration spell). |
| M-35 Reveal | Ort An Quas | Reveals hidden objects and concealed exits from current location (instantaneous spell). |

### 8th Circle

| ID | Spell | Formula | Behavior |
| --- | --- | --- | --- |
| M-36 Flame Wind | Flam Hur | Casts multiple flaming missiles into the area (instantaneous spell). |
| M-37 Freeze Time | An Tym | Stops the flow of time for all but you (duration spell). |
| M-38 Iron Flesh | In Vas Sanct | Greatly increases your resistance to damage (duration spell). |
| M-39 Roaming Sight | Ort Por Wis | Allows you to see the world from a bird's-eye view (duration spell). |
| M-40 Tremor | Vas Por Ylem | Causes the ground to quake and rocks to burst (instantaneous spell). |

Grimoire-adjacent manual facts: Night Vision / Daylight / Light substitute for
torches and candles (pp. 6, 10–11); Create Food answers hunger (p. 19); flying
via spell or item uses `E` ascend / `Q` descend-land (p. 21); area spells can
destroy nearby valuables (Flame Wind warning, p. 27).

## 2. The 24 runes (p. 32)

| Rune | Gloss | Rune | Gloss | Rune | Gloss | Rune | Gloss |
| --- | --- | --- | --- | --- | --- | --- | --- |
| R-01 AN | Negate | R-09 IN | Cause | R-17 QUAS | Illusion | R-21 UUS | Raise |
| R-02 BET | Small | R-10 JUX | Harm | R-18 REL | Change | R-22 VAS | Great |
| R-03 CORP | Death | R-11 KAL | Summon | R-19 SANCT | Protection | R-23 WIS | Knowledge |
| R-04 DES | Down | R-12 LOR | Light | R-20 TYM | Time | R-24 YLEM | Matter |
| R-05 EX | Freedom | R-13 MANI | Life | — | — | — | — |
| R-06 FLAM | Flame | R-14 NOX | Poison | — | — | — | — |
| R-07 GRAV | Energy | R-15 ORT | Magic | — | — | — | — |
| R-08 HUR | Wind | R-16 POR | Movement | — | — | — | — |

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

Condensed from the printed list — substance checked, wording mine.

| ID | Skill | Attr | Role |
| --- | --- | --- | --- |
| S-01 | Acrobat | DX | Move with grace; reduces fall/collision damage |
| S-02 | Appraise | DX | Perceive goods' value; aids barter evaluation |
| S-03 | Attack | ST | General fighting; bonus to hit |
| S-04 | Axe | ST | Axes; defense + bonus to hit with axes |
| S-05 | Casting | INT | Improves cast success |
| S-06 | Charm | DX | Making friends; better barter deals |
| S-07 | Defense | ST | Penalty to foes trying to strike |
| S-08 | Lore | INT | Identify items; accuracy of Look information |
| S-09 | Mace | ST | Blunt weapons; defense + bonus to hit |
| S-10 | Mana | INT | Increases maximum Mana |
| S-11 | Missile | ST | Bows/crossbows/slings; increases missile damage |
| S-12 | Picklock | DX | Lockpick use on doors/chests |
| S-13 | Repair | DX | Anvil repair success |
| S-14 | Search | DX | Detect hidden doors/traps; auto-applied on Look |
| S-15 | Sneak | DX | Move quietly automatically |
| S-16 | Swimming | DX | Postpones drowning |
| S-17 | Sword | ST | Swords/daggers; defense + bonus to hit |
| S-18 | Track | DX | Perceive tracks; tells when creatures are near |
| S-19 | Traps | DX | Disarm found traps |
| S-20 | Unarmed | ST | Bonus to hit and damage with fists |

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
| 12 | "Not castable here" (debug-print + break inline in `spellcasting.cs`, no class file) |
| 13 | Misc (Altaras wand, mind blast, …) | `spellcasting_class_13.cs` |
| 14 | Cutscene (via `cutsplayer.PlayCutscene`, no class file) |

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
