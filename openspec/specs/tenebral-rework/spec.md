# Tenebral Rework Specification

## Purpose
Tenebral bionics are a stepping stone on the road to Umbral. Today they cause psychosis (see `tenebral-bloodlust`). This rework tracks how often a psychosis-afflicted pawn breaks and kills. Breaking, the price of refusing to kill, offers a choice of the Orientis path (see `orientis-faction`). Killing crystallizes the pawn on the road to Umbral (see `umbral-rework`). Tenebral production moves from the regular buildings to the Umbral line, with a new Dark Archotech Altar as an additional way to make tenebral items. Umbral buildings produce the same items normally.

## Requirements

### Requirement: Hidden break and kill counter
A pawn with tenebral psychosis SHALL have a hidden hediff that counts the mental breaks that pawn has suffered while psychotic and the humanlikes it has killed as a tenebral. The counter SHALL NOT be shown to the player. A kill SHALL count only when the killer is a tenebral (a humanlike of the player's faction, psychotic, with at least one tenebral implant) and not Umbral.

### Requirement: Orientis choice
When a counted break ends for a pawn that has reached the Orientis threshold, the player SHALL be offered the once-per-colony choice between the Path of Light and the Path of Dark, as defined in `orientis-faction`.

#### Scenario: Choice offered
- WHEN a pawn reaches the Orientis threshold, the colony has not chosen a path, and the mental state ends
- THEN the player receives the choice letter

### Requirement: No manual stripping
The player SHALL NOT be able to remove tenebral parts by any operation at any point. This SHALL cover removing the part, replacing it by installing another part, and removing a parent part it is attached to. The only ways a tenebral part leaves a pawn SHALL be the Path of Light (`orientis-faction`), emergence as Umbral, and injury.

#### Scenario: Surgery after the threshold
- WHEN a pawn holding a tenebral arm is past any number of breaks
- THEN no operation is offered that removes or replaces the arm or the shoulder it is attached to

### Requirement: Crystallization
The pawn's brain crystallization SHALL be 50% x counted kills / (1000 - 100 x tenebral parts the pawn carries now), with the divisor never below 1, from the first counted kill. Reaching 50% therefore takes 1000 counted kills minus 100 for each tenebral part; kills only count with at least one tenebral part, so it is never more than 900. Parts change the baseline, not the kills: installing a tenebral part SHALL raise crystallization at once, and losing one SHALL lower it. Breaks SHALL NOT crystallize anything.

- Only the brain crystallizes. No other body part SHALL crystallize.
- Brain crystallization SHALL NOT be removable by surgery.
- Brain crystallization SHALL reduce the brain's efficiency by its own percentage. It SHALL NOT disable the brain outright, and it has no cap: a fully crystallized brain kills.

#### Scenario: First kill
- WHEN a tenebral colonist with one tenebral part makes the first counted kill
- THEN the brain is 50%/900 (about 0.06%) crystallized and the "crystallizing" letter is sent

#### Scenario: Installing parts
- WHEN a tenebral colonist with 100 counted kills and two tenebral parts (800 kills to 50%) has two more installed
- THEN brain crystallization goes from 6.25% (50% x 100/800) to 8.33% (50% x 100/600) without another kill

#### Scenario: Half crystallized
- WHEN the pawn's brain is 50% crystallized
- THEN the pawn can be placed in the dark mirror in transformation mode

### Requirement: Transformation mode hold
A pawn placed in the dark mirror set to transformation mode SHALL be held for `(100 - crystallized%)`% of a year. The hold SHALL NOT end early, except that the Path of Light destroys the pod and kills the pawn (see `orientis-faction`).

#### Scenario: Partial crystallization
- WHEN the pawn is 50% crystallized
- THEN the hold lasts 50% of a year

### Requirement: Emergence as Umbral
When the hold ends, the pawn SHALL emerge as Umbral, as defined in `umbral-rework`.

### Requirement: Building split
Every Tenebral building SHALL be replaced by an Umbral building, except the turret, which SHALL remain Tenebral. The Umbral tier has no items of its own. Its buildings (tier IV) SHALL produce the tenebral-tier items (weapons, guns, apparel, vaccines, organs, appendages) as well as the items moved to the Umbral line (drugs and medicine). Umbral pawns resist tenebral psychosis, so they cannot use the Dark Archotech Altar and SHALL NOT need it.

### Requirement: New Tenebral buildings
Tenebral SHALL gain the Dark Archotech Altar. The Umbral transformation chamber is a mode of the tenebral pod, which is part of the dark mirror, not a separate building.

### Requirement: Dark Archotech Altar
The Dark Archotech Altar SHALL be a special Forge whose grid is prefilled with tenebral items: guns, apparel, tenebrite, tenebral organ, tenebral appendage, femtoclusters, and the tenebral and umbral vaccines. Tenebral melee weapons are not defined by this mod; they come from AllBambaMelee, which adds them to the grid when it is loaded. Every item on the altar SHALL also have a normal recipe at the Umbral tier IV buildings; the altar is an additional source, not the only one.

- Every grid cell SHALL start prefilled but locked.
- A locked cell SHALL be unlocked only by performing work on it.
- Unlocking SHALL be shared by every altar: an item unlocked at one altar SHALL be unlocked at every altar on every map, including altars built later. Which cells the player asked to unlock, and unlock progress, SHALL be shared the same way.
- The altar SHALL be usable only by a pawn with the Tenebral Psychosis hediff.
- Analysis and production on the altar SHALL drain the pawn's Bloodlust need.
- Progress SHALL persist, so work can be paused and resumed. (Unlike a regular forge.) Production progress SHALL stay on the altar.

#### Scenario: Second altar
- WHEN an item has been unlocked at one altar and the player builds a second altar
- THEN the item is already unlocked at the second altar

#### Scenario: Requesting an item
- WHEN the player brings the scaled amount of crystalline joints (componentIII) to the altar
- THEN the altar "requests" the item

### Requirement: Tenebrite exposure
Everything created with `BOR_Tenebrite` (buildings, floors, walls, weapons, armors) SHALL cause tenebral psychosis to grow temporarily in nearby pawns, even without tenebral bionics. Growth SHALL be delayed by a few days, similar to how pollution affects pawns.

- Buildings, floors, and walls SHALL apply psychosis by emission, like pollution.
- Weapons and armors SHALL apply psychosis directly by applying the hediff to the wearer or holder. They SHALL work like a bionic, with the ratio to be decided during implementation.

#### Scenario: Pawn without bionics
- WHEN a pawn without tenebral bionics stands near a tenebrite-made building or wears tenebrite armor
- THEN tenebral psychosis grows in that pawn after a few days

### Requirement: Tenebral pod
The tenebral pod SHALL be part of the dark mirror (`BOR_Research_Unlocker_III`, class `Building_TenebralPod`), which also hosts dark pacts; there is no separate pod building. It combines a bionic installator, a healing pod, and a stasis pod. It SHALL provide a mode gizmo with four modes: stasis, healing, operating, and transformation. The mode cannot be changed while a transformation is under way.

The pod SHALL be locked until `BOR_Research_Medicine_III` is finished: the gizmo is disabled and no pawn can enter. The mirror SHALL have no mode until the player picks a mode and a pawn in the pod window, and SHALL drop back to no mode once nobody is inside or on the way, no operation is set up and no transformation runs. With no mode, no pawn can enter.

The pod SHALL run on the mirror's power. Without power (cut, switched off, broken down) no pawn can enter, whoever is inside is ejected at once, and a transformation in progress is aborted: the pawn comes out crystallized, not Umbral, and can go back in to start over. An operation that is set up stays set up with its ingredients.

#### Scenario: Back to no mode
- WHEN a pawn ordered into stasis is drafted before reaching the mirror
- THEN within a rare tick the mirror has no mode again
- WHEN an operation is set up and its target walks away
- THEN the mirror stays in operating mode and keeps the loaded ingredients

#### Scenario: Look
- The portal floats over the button base and bobs; it phases from dark to lit while the mirror has a mode and power, and back when it loses either. The noise dissolve and its red edge play only on the crystal (the area the `aroused` glow covers); the case around it stays put
- A glow pulses over it while it has a mode and someone is inside

#### Scenario: Healing mode
- WHEN a tenebral pawn (tenebral implants or a crystallizing brain) or an Umbral enters the pod in healing mode
- THEN each of their injuries heals at 10 times the natural rate
- WHEN any other pawn is picked for healing
- THEN the pod refuses them (`BOR_TenebralPodNeedsTenebral`); these are the same pawns dark medicine makes sick

#### Scenario: Stasis mode
- WHEN a pawn without a tenebral hediff enters the pod in stasis mode
- THEN the pawn is frozen, like a cryptosleep pod, and the player must take them out manually

### Requirement: Operating mode
Before the pod can operate, a tenebral appendage or organ and femtoclusters SHALL be placed inside. The player SHALL then select where and what to install. Installation SHALL be a single step, and its duration SHALL come from the existing recipe work amount.

### Requirement: Pawn card in the pod window
Hovering a pawn in the pod window's colonist, slave or prisoner lists SHALL show a card next to the mouse (`TenebralPodPawnCard`, a floating window so it is not clipped by the pod window) with: portrait, name, role, age and non-baseliner xenotype; health (downed, bleeding); crystallization against the transformation threshold; tenebral psychosis severity if any; installed tenebral implants (hediffs added by the pod's operations); other visible bad conditions (up to 6, then a count); why the pawn can't be picked for the current action, if so; and whether they would walk or be carried to the pod. It SHALL be drawn through BorSkin in the Tenebral scheme.

## Open questions
- Whether a pawn can be placed in transformation mode before reaching 50%.
- Whether crystallization progresses while the pawn is not psychotic.
- The scaling formula for crystalline joints (componentIII) per requested item.
- How many work actions unlock each locked cell on the altar.

## Implementation decisions
Implemented in `TenebralPsychosis.cs`, `TenebralSurgery.cs`, `TenebralPods.cs`, `DarkAltar.cs`. Tunables live in XML mod extensions on `BOR_TenebralPsychosis` and `BOR_TenebralBreakCounter`.

- Psychosis is the `BOR_TenebralPsychosis` hediff (severity 0..1). It is hidden below 0.1 (latent), which is the "delayed by a few days" part. A pawn is psychotic once it is visible.
- Severity floor = (implants + tenebrite gear x 0.5) x 0.1. Nearby tenebrite emission (buildings 1, floor cells 0.1, radius 6, capped at 3) adds 0.03 severity per day per unit. Without emission, severity decays 0.05/day down to the floor.
- Orientis threshold: 3 breaks. Breaks caused by damage or psycasts don't count. There is no kill threshold: every counted kill crystallizes.
- Surgery: a part can't be removed or replaced if it or anything below it is tenebral or crystallized, and a tenebral implant can't be removed. Implants that sit beside a part can still be installed on a part with tenebral below it, including a crystallizing brain; the head around a crystallizing brain can't be removed or replaced.
- Crystallization: the `BOR_Crystallized` hediff on the brain, severity 0..1, which takes the same amount off the brain's efficiency (`partEfficiencyOffset = -severity`). Severity = `podThreshold x kills / max(1, killsToPodThreshold - killsSavedPerPart x tenebral parts)`, that is 50% x kills / (1000 - 100 x parts). The kill counter is the source of truth: it is recalculated on every counted kill and whenever the counter notices (checked every 60 ticks) that the pawn's tenebral part count changed. The first counted kill sends the "crystallizing" letter. With 10 or more parts one kill reaches 50% and the next is fatal; installing enough parts on a pawn with many kills can likewise push them past 70% (unconscious) or to 100% (dead) at once. It is never capped. There is no timer, so a pawn who stops killing stops crystallizing. Every 10 points (`abilityRollEvery`) gives a dark ability roll, once per step: the counter remembers the highest step paid (`rolledSteps`), so losing and reinstalling a part does not roll again. On load, a crystal that is not backed by kills (older saves, migrated parts) raises the kill counter to the kills that give at least that level with the current parts, and its earned rolls count as paid. The pod, the pod card and the Into the dark pact accept 50% within 0.0001, so float drift over hundreds of kills can't hold back the kill that should reach it. Saves from the per-part rule have their crystallized parts removed on load and replaced by brain crystallization of (parts / 11), the old human share. The Orientis path clears brain crystallization without a golden scar.
- Transformation mode refuses a pawn below 50%.
- Altar: unlocking a cell takes the forge analysis time for the item. Each run costs ceil(value x yield x 0.5 / joint value) crystalline joints (at least 1). Work drains 2 days' worth of bloodlust per day of work.
- Altar unlocks live in `GameComponent_DarkAltar`, keyed by the item's defName, so an item from a removed mod loads without errors. Saves from before kept unlocks on each altar; on load, an item unlocked or requested at any altar carries over to all of them, with the furthest progress.
