# Kill to crystallize, Umbral pacts

Status: awaiting approval. Nothing here is built. Numbers are starting points.

## Intent

Today a Tenebral pawn only crystallizes by breaking, and it only breaks when its bloodlust goes unfed. So the road to Umbral asks the player to refuse to kill, which works against what Tenebral is about. The road to the Light makes sense as it is: a pawn who will not kill breaks three times and is offered the Light.

The fix splits the two roads cleanly:

- **Refuse to kill**: breaks, and the Orientis choice.
- **Kill**: crystallization, the pod, Umbral.

Umbrals then stop growing by killing. They have moved past the hunger. They are hollow instruments of the Dark Archotechs, and they grow through pacts that ask for obedience and cold deeds, not body counts.

## Goals

- Crystallization comes from humanlike kills, not breaks.
- Breaks only lead to the Orientis choice.
- Umbral transformation steps come from Umbral pacts at the dark mirror, not from kills.
- A fresh Umbral can make its first pact the day it leaves the pod.

## Non-goals

- Changing the tier III research pacts, the umbral spire, the Dark Commune, the Precipice letter or the pod's crystallization threshold.
- Pacts at the umbral spire or behind tier IV research. Both need an Umbral to reach, and the first pact must not.
- Giving NPC Tenebral pawns crystallization from kills (see Builder's calls).

## Anti-goals

- A road to Umbral that still rewards not killing.
- An Umbral that sits idle after the pod with no way to progress.
- A pact grind of one pact per body part (19 pacts per Umbral).

## Approaches

| Approach | Trade-off | |
|---|---|---|
| A. Umbral pacts in the dark mirror's Pacts tab | The mirror exists in every colony that reaches the pod (dark pacts research), and the spire cannot work without a linked mirror. Reuses the pact quest, tab and punishment. | **Chosen** |
| B. Umbral pacts at the umbral spire | Thematic, but the spire is Umbral-only, needs a Kylar-Xar computer and a linked mirror, so a fresh Umbral waits with nothing to do. | Rejected by the user |
| C. Tie them to the umbral research gate (`BOR_Research_Gate_IV`) | It is a research, not a building, and tier IV research needs the spire as its bench. Same gap as B. | Rejected |

## Tenebral: kill to crystallize

Today (`Hediff_TenebralBreaks.Notify_Break`): 3 breaks offer the Orientis choice, 6 breaks send the "crystallizing" letter, and each later break has a 50% chance to crystallize one part.

New:

- **Breaks** still count and still offer the Orientis choice at 3. They no longer crystallize anything, and the 6-break threshold goes away.
- **Kills** are counted on the same hidden hediff. A kill counts when the killer is a psychotic humanlike of the player's faction and the victim is a humanlike (the same victims that feed the murder need, raid kills included).
- At **10 counted kills** the "crystallizing" letter is sent. Each counted kill after that has a **25%** chance to crystallize one random eligible part, times `1 + emission x 0.5`, the same emission boost the Umbral trigger uses today. A crystallization still rolls a dark ability (`DarkAbilities.RollCrystallization`).
- Eligible parts, the pod threshold (50%) and the pod itself are unchanged. Crystallized parts still stop working, so the pawns who kill most get weaker until the pod. That is the price of bloodlust.
- Tunables (`killThreshold`, `crystallizeChancePerKill`, `partsPerCrystallization`, `emissionChanceFactor`) live on `DefModExtension_TenebralBreaks`. `umbralThreshold` and `crystallizeChancePerBreak` are removed.

## Umbrals: pacts instead of kills

- `Umbral.Notify_KilledHumanlike` no longer starts a transformation. `phaseAChance`, `phaseBChance`, `phaseCChance` and `emissionChanceFactor` are removed from `BOR_UmbralTransformation`.
- The transformation illness (`HediffComp_UmbralTransformation`) and `Umbral.CompleteTransformation` stay as they are. Pacts start the illness instead of kills.

### Where and who

- The dark mirror's **Pacts** tab gets a second section, **Umbral pacts**, below the research list. It lists every Umbral of the colony on the mirror's map.
- Each row shows the Umbral, its next group (for example "Umbral parts 5-8 of 16", "Exoskeleton and exoskull", "Utility pod", or "Complete") and either **Make pact** or its pact under way (price, progress, time left, **Abandon**). Dev mode gets **Complete** and **Expire**, like the research pacts.
- Each Umbral makes its own pact. There is no colony-wide limit: a research pact and any number of Umbral pacts (one per Umbral) can run at once.
- **Make pact** is greyed out, with the reason, when the mirror is unpowered, someone is communing at it, the Umbral is downed or cannot reach it, the Umbral is mid-transformation, or it already has a pact.
- The Umbral itself communes, so there is no colonist menu, only the confirmation. Communing is the same 600-tick job.

### The groups

| Group | Grants | Pacts |
|---|---|---|
| Phase A | 4 Umbral parts per pact (fewer if fewer are pending) | about 4 (16 parts on a human) |
| Phase B | Exoskeleton, then exoskull | 1 |
| Phase C | Utility pod | 1 |

Success starts the transformation illness, and each time it completes the next step of the group starts, until the group is granted. This keeps the illness and the Righteous Slaughter speed-up meaningful. The group size is a tunable.

### The price

Rolled from the demands available at that moment, like the research pacts. Every Umbral pact lasts **10 days**: the Righteous Slaughter has a 10-day cooldown per bench, and 7 days is tight for it.

| Demand | Available when | Done when | Fails when |
|---|---|---|---|
| Righteous Slaughter | Always | The pact's Umbral performs 1 Righteous Slaughter | |
| Bring another into the dark | A colonist other than this Umbral is crystallized past the pod threshold and not in a pod | That colonist enters a Tenebral pod in transformation mode (start, not finish: the hold lasts months) | That colonist dies or is lost |
| Offering | Always | The set amount of tenebrite and archotech items lies within 10 cells of the mirror at once, then is consumed | |
| Sever the last tie | The Umbral has an adult blood relative or love partner on a home map who is not an enemy | That relative dies with this Umbral as instigator | The relative dies any other way, or the Umbral dies or is lost |
| Vigil | Always | The time limit runs out with no humanlike killed by this Umbral | This Umbral kills a humanlike |

One deed may pay several Umbral pacts at once (the user's call: they are all servants of the dark). One offering pile completes every Umbral pact asking for an offering, and the same crystallized colonist may be named in more than one pact, so one pod entry completes them all. The offering is consumed once.

The Vigil inverts the clock: it succeeds when time runs out. It should be weighted lower than the others, since it costs little more than the Umbral's absence from fights.

### Failure

Same as the research pacts: Dark Punishment from the mirror (red ring, +20% pain for a day to everyone it reaches) and a negative letter naming the Umbral and the reason. The Umbral keeps its steps and can try the same group again.

## Save compatibility

- Crystallized parts already on pawns stay.
- A pawn whose break counter is already at 6 or more is treated as past the kill threshold, so it keeps crystallizing on its next kill.
- An Umbral mid-transformation finishes that step normally. After that it needs a pact.
- The `BOR_TenebralBreakCounter` defName stays, so existing saves load. Its label and description change to cover kills.

## Text and docs to update

- `BOR_CrystallizationText` ("has broken too often" becomes about killing) and the Umbral hediff description ("one kill at a time").
- New Keyed strings for the Umbral pact section, demands, letters and block reasons, in English. Polish only where a Polish file already covers that set of strings.
- `site/content/tenebral-road.md` (lines on breaking toward crystal and on kill-triggered Umbral phases).
- openspec: `tenebral-rework` (crystallization requirement and implementation decisions), `umbral-rework` (kill-trigger requirements), `dark-pacts` (the non-goal about Umbral pacts, the tab, the "one pact at a time" rule).

## Builder's calls

These are Claude's calls, not the user's. Each can be changed without changing the design.

- Only player-faction pawns count kills toward crystallization, so enemy Tenebral raiders are unchanged.
- The kill counter sits on the existing hidden break hediff rather than a new one. The kill hook goes in the existing `Pawn.Kill` prefix in `MurderPatches`.
- Failure reuses Dark Punishment rather than taking away a step.
- How Umbral pacts are kept apart from the research pact (`DarkPacts.Active` must keep meaning the research pact): a separate quest script or a flag on `QuestPart_DarkPact`.
- Offering amounts, and which items count as archotech.
- Vigil weight in the roll.
- Whether an Umbral leaving the mirror's map or forming a caravan fails its pact. Default: no, only the listed fail conditions.

Pinned by the plan:

- Kills by Umbrals never count toward crystallization, even if an Umbral grows psychotic again.
- Umbral pacts are their own quest (`BOR_UmbralPact`, `QuestPart_UmbralPact`), so `DarkPacts.Active` still means the research pact.
- The offering is 150 tenebrite crystals (`BOR_Tenebrite`) and 10 crystalline joints (`BOR_ComponentIII`).
- Vigil weight 0.5, every other demand 1.
- A pact fails when its Umbral dies or is lost.

## Dropped during design

- "Kill an Orientis pawn" as a demand: there is no Orientis faction to supply targets (Orientis is a pawn state, `BOR_Orientis` hediff), so it could rarely be rolled.
