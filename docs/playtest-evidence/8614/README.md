# Task 8614: Crew GPU run

Observed 2026-09-25 on the conversation revision (`b322d40`) with the product
restarted on it. Profile `rusty-underworld`, Wolf/Gamescope, 1280×720 stream.
The level, the item catalog, the conversations and the strings are all the
operator's own import (`scripts/import-level.sh` reads CNV.ARK and STRINGS.PAK
alongside the level).

Talk is gameplay input: the `E` key is the declared use action. Two `abyss.goto`
calls stand the avatar next to a creature the import placed, which is the same
operator tooling the other evidence records use (`docs/gpu-playtesting.md`).

## Result

**The avatars of the Abyss talk, in their own words.** The import carries 97
conversation scripts and 99 string blocks from the operator's data; a creature's
own record names the conversation it holds, and the use channel runs that script
on the conversation VM, whose transcript and panel reach the DOM.

- [A conversation in the imported words](01-conversation-imported-dialogue.png) —
  artifact `406f11ad-b868-4e3d-b622-0f1160b75836`, session
  `70a41b5b-6c05-4015-b7fd-1d7fd8b7e3e1`. Standing next to the NPC the import
  places on tile (17,7), the use key runs its script: the panel carries the
  creature's account of the prison it escaped, the Banner of Cabirus and the way
  out, exactly as the operator's CNV.ARK has it, ending `Good luck in thy
  travels.`
- [A named creature answers](02-named-npc-drog.png) — artifact
  `7e2353ba-7c85-4dba-bd98-7b9b89823d06`, session
  `34d627ee-1d31-46d1-a19a-e9d81e3f09c2`. On tile (15,35) the panel names the
  speaker from the conversation string block — `DROG` — and shows what it says:
  `Yes? What your business here? Well, intrude you did! Out now!`

Barter rides the same path rather than a second one: all 97 imported
conversations call the trade imports (`setup_to_barter`, `do_offer`), the tray is
built from the imported monetary values of what each side carries, and the
panel's title carries the trade result when one resolves. The focused Host test
`A_vendors_own_script_trades_against_what_each_side_carries` pins an accepted
trade end to end through a script of the game's own opcodes.

## What this run does not show

A conversation is one pass: the script runs to its end and the panel publishes
what it said and the options it offered, but the avatar cannot yet choose an
option, so the talk cannot continue past its first question. #8623 carries that
turn-taking. The panel also shows a script's options as the numbered list the
donor's menu produces; where a script asks for typed input the panel shows `>`.
Placed creatures are still not drawn (#8592), so the frames show the level and
the panel rather than the speaker. The GPU timer is unavailable on this host, so
frame pacing is submission evidence only.
