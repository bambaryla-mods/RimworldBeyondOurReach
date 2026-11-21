# Kill to Crystallize, Umbral Pacts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task (the user runs plans Native). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tenebral pawns crystallize by killing instead of by breaking, and Umbrals gain transformation steps from per-Umbral pacts at the dark mirror instead of from kills.

**Architecture:** The hidden break counter hediff also counts kills and owns crystallization. Umbral kills stop starting the transformation illness. A new quest (`BOR_UmbralPact`, its own root node and `QuestPart_UmbralPact`) rolls a demand, and success starts the existing illness with a step count so one pact grants a group of steps. The dark mirror's Pacts tab gets an Umbral section, and the communion job gets an Umbral-pact variant.

**Tech Stack:** C# (`Source/BeyondOurReach.csproj`, builds to `Assemblies/`), Harmony, RimWorld 1.7 XML defs, Keyed strings.

**Spec:** `docs/superpowers/specs/2026-11-21-kill-to-crystallize-umbral-pacts-design.md`

**Testing:** the mod has no unit test harness, so it can't follow red-green TDD. Each task is verified by a clean build (`dotnet build Source/BeyondOurReach.csproj -c Release`, 0 errors) plus a short in-game dev-mode check listed in the task. Keep the debug actions this plan adds: they are what make the checks runnable.

## Global Constraints

- RimWorld 1.7 only, no per-version folders.
- Everything in the `BeyondOurReach` namespace. New Umbral code goes in `Source/Umbral/`, Tenebral code in `Source/Tenebral/`.
- Straight quotes only in all text.
- English Keyed strings only. No Polish file covers `BOR_DarkPacts.xml` or `BOR_TenebralUmbral.xml` strings, so none is added.
- Commits: plain messages, no AI attribution, no `Co-Authored-By`.
- `BOR_TenebralBreakCounter` keeps its defName (save compatibility).
- Numbers: kill threshold 10; crystallize chance 25% per kill past it, times `1 + emission x 0.5`; Phase A group 4 parts; Umbral pact lasts 10 days; Vigil roll weight 0.5 (others 1); offering 150 `BOR_Tenebrite` + 10 `BOR_ComponentIII` within 10 cells of a colony dark mirror; Righteous Slaughter demand 1.

## Review Focus

- **An Umbral who becomes psychotic again** (tenebrite exposure) must not count kills or crystallize. Covered: Task 1 `CountsKill` excludes Umbrals, check step 5.
- **Old save, pawn past 6 breaks** keeps crystallizing on its next kill; **new save, pawn reaching 6 breaks** does not get kill credit. Covered: Task 1 `migrated` flag, check step 5.
- **Old save, Umbral mid-transformation** (no step count saved) finishes one step and stops. Covered: Task 2, check step 4.
- **The pact's Umbral leaves the map** (caravan, kidnapped): the pact keeps running and stays visible and abandonable in the tab. Covered: Task 4 row list includes pact holders off the map, check step 5.
- **A research pact and Umbral pacts at once**: `DarkPacts.Active` and the research rows ignore Umbral pacts, and the communion lock covers both jobs. Covered: Task 4, check step 5.

---

### Task 1: Tenebral pawns crystallize by killing

**Files:**
- Modify: `Source/Tenebral/TenebralPsychosis.cs:42-57` (`DefModExtension_TenebralBreaks`), `:318-379` (`Hediff_TenebralBreaks`), `:476-486` (berserk patch)
- Modify: `Source/Tenebral/MurderNeed.cs:147-161` (`Pawn_Kill_Patch`)
- Modify: `Source/Orientis/OrientisPath.cs` (`OrientisDebug`)
- Modify: `Common/Defs/HediffDefs/Hediffs_Tenebral.xml:64-80`
- Modify: `Languages/English/Keyed/BOR_TenebralUmbral.xml:5`

**Interfaces:**
- Produces: `TenebralPsychosis.BreakCounter(Pawn pawn) -> Hediff_TenebralBreaks` (get or add); `TenebralPsychosis.CountsKill(Pawn killer) -> bool`; `Hediff_TenebralBreaks.Notify_Kill()`; field `Hediff_TenebralBreaks.kills`.

- [ ] **Step 1: Tunables.** In `DefModExtension_TenebralBreaks` remove `umbralThreshold` and `crystallizeChancePerBreak`, and add `public int killThreshold = 10;`, `public float crystallizeChancePerKill = 0.25f;`, `public float emissionChanceFactor = 0.5f;`, each with a one-line comment. In `Hediffs_Tenebral.xml` swap the two removed elements for `<killThreshold>10</killThreshold>`, `<crystallizeChancePerKill>0.25</crystallizeChancePerKill>` and `<emissionChanceFactor>0.5</emissionChanceFactor>`. Set label to `tenebral breaks and kills`, description to `Hidden counter of mental breaks and kills under tenebral psychosis.`, and fix the XML comment the same way.

- [ ] **Step 2: Counter.** In `Hediff_TenebralBreaks`:
  - `Notify_Break()` keeps `breaks++` and the Orientis `choicePending` logic, and loses everything about crystallization.
  - Add `public int kills;` and `private bool migrated;`. Set `migrated = true` in a `PostAdd` override.
  - Add `public void Notify_Kill()`: `kills++`. When `kills == killThreshold`, send the existing `BOR_CrystallizationLabel`/`Text` letter (player faction only, as now). When `kills > killThreshold` and `Rand.Chance(crystallizeChancePerKill * (1f + MapComponent_TenebralExposure.EmissionFor(pawn) * emissionChanceFactor))` succeeds, run the crystallize loop moved from `Notify_Break` (`partsPerCrystallization` parts, then `DarkAbilities.RollCrystallization` if any part turned).
  - `ExposeData`: scribe `kills` and `migrated`. On `PostLoadInit`, if `!migrated`: when `breaks >= 6` (a `const int LegacyUmbralBreaks = 6` with a comment naming the old rule) and `kills < killThreshold`, set `kills = killThreshold`. Then set `migrated = true`.
  - Update the class comment.

- [ ] **Step 3: Hooks.** Add both statics to `TenebralPsychosis`. `BreakCounter` is the get-or-add now inlined in `MentalStateHandler_TryStartMentalState_Patch`; make that patch call it. `CountsKill` is true when `killer.RaceProps.Humanlike && killer.Faction == Faction.OfPlayer && !Umbral.IsUmbral(killer) && IsPsychotic(killer)`. In `MurderPatches.Pawn_Kill_Patch.Prefix`, replace `Umbral.Notify_KilledHumanlike(killer);` with `if (TenebralPsychosis.CountsKill(killer)) TenebralPsychosis.BreakCounter(killer).Notify_Kill();`, and update the comment above the patch ("sates the bloodlust and counts toward crystallization"). In `OrientisDebug`, next to `ForceBreak`, add `[DebugAction("Pawns", "Add tenebral kill", ...)] AddTenebralKill(Pawn)`. It calls `BreakCounter(pawn).Notify_Kill()` for humanlikes and does not check `CountsKill`, so it can force the count.

- [ ] **Step 4: Text.** `BOR_CrystallizationText` opens with `{PAWN_labelShort} has killed too often under the psychosis.`. The rest stays the same.

- [ ] **Step 5: Build and check.**
  Run: `dotnet build Source/BeyondOurReach.csproj -c Release`. Expected: `0` errors.
  In-game (dev mode), on a psychotic colonist:
  - "Force tenebral break" 7 times: no crystallization, and the Orientis letter at 3.
  - "Add tenebral kill" 10 times: the crystallizing letter on the 10th. Further kills crystallize roughly 1 in 4.
  - An Umbral pawn shooting a prisoner gains no crystals.
  - Load a save made before this change with a pawn past 6 breaks: its next "Add tenebral kill" can crystallize.

- [ ] **Step 6: Commit.**
```bash
git add Source/Tenebral/TenebralPsychosis.cs Source/Tenebral/MurderNeed.cs Source/Orientis/OrientisPath.cs Common/Defs/HediffDefs/Hediffs_Tenebral.xml Languages/English/Keyed/BOR_TenebralUmbral.xml Assemblies/BeyondOurReach.dll
git commit -m "Tenebral pawns crystallize by killing, not by breaking"
```

### Task 2: Grouped Umbral transformation steps

**Files:**
- Modify: `Source/Umbral/Umbral.cs:90-114` (remove `TriggerChance`, `Notify_KilledHumanlike`), `:259-277` (`HediffComp_UmbralTransformation`)
- Modify: `Source/Umbral/UmbralDefs.cs:23-35` (`DefModExtension_UmbralTransformation`)
- Modify: `Common/Defs/HediffDefs/Hediffs_Umbral.xml:7` (description), `:98-102`

**Interfaces:**
- Consumes: `Umbral.CurrentPhase`, `Umbral.PendingReplacements`, `Umbral.CompleteTransformation` (existing).
- Produces: `Umbral.GroupSteps(Pawn pawn) -> int` (steps the next pact grants, 0 when done); `Umbral.StartGroup(Pawn pawn)` (starts the illness carrying `GroupSteps - 1` more); `Umbral.Transforming(Pawn pawn) -> bool` (has `BOR_UmbralTransformation`); `HediffComp_UmbralTransformation.stepsLeft`; extension field `partsPerPact`.

- [ ] **Step 1: Remove kill triggers.** Delete `TriggerChance` and `Notify_KilledHumanlike`. Delete `phaseAChance`, `phaseBChance`, `phaseCChance` and `emissionChanceFactor` from the extension and the XML, and add `public int partsPerPact = 4;` / `<partsPerPact>4</partsPerPact>`. The `BOR_Umbral` description ends `...changes on its own, one pact at a time.`

- [ ] **Step 2: Groups.** `GroupSteps`: Phase A gives `min(partsPerPact, PendingReplacements(pawn).Count())`; Phase B gives the number of exoskeleton and exoskull still missing (1 or 2); Phase C gives 1; Done gives 0. A group never crosses a phase. `StartGroup` adds `BOR_UmbralTransformation` and sets its comp's `stepsLeft = GroupSteps(pawn) - 1`. In `HediffComp_UmbralTransformation` add `public int stepsLeft;` and scribe it in `CompExposeData`. On completion, read `stepsLeft` before removing the parent, call `CompleteTransformation`, and if `stepsLeft > 0` and the pawn is not Done, add a fresh hediff with `stepsLeft - 1`. Old saves load `stepsLeft = 0`, so they finish one step and stop.

- [ ] **Step 3: Debug action.** Add `[DebugAction("Pawns", "Start Umbral pact group", ...)]` (in `Umbral.cs`, a small `UmbralDebug` static class) that calls `StartGroup` on an Umbral that is not `Transforming`.

- [ ] **Step 4: Build and check.**
  Run: `dotnet build Source/BeyondOurReach.csproj -c Release`. Expected: `0` errors, and a grep for `TriggerChance|phaseAChance|Notify_KilledHumanlike` over `Source` and `Common` finds nothing.
  In-game:
  - An Umbral killing prisoners never starts the illness.
  - "Start Umbral pact group" on a fresh Umbral grants 4 parts one after another, then stops.
  - Repeat until Phase A is done. Then one group gives exoskeleton and exoskull, and the next gives the utility pod.
  - An old-save Umbral that was mid-illness gets one step.

- [ ] **Step 5: Commit.**
```bash
git add Source/Umbral/Umbral.cs Source/Umbral/UmbralDefs.cs Common/Defs/HediffDefs/Hediffs_Umbral.xml Assemblies/BeyondOurReach.dll
git commit -m "Umbral transformation comes in pact-sized groups, not from kills"
```

### Task 3: The Umbral pact quest

**Files:**
- Create: `Source/Umbral/UmbralPact.cs` (enum, `UmbralPacts` statics, `QuestNode_Root_UmbralPact`, `QuestPart_UmbralPact`)
- Create: `Common/Defs/QuestScriptDefs/UmbralPact.xml`
- Modify: `Source/Tenebral/DarkPactQuest.cs` (`QuestPart_DarkPact.Fail` uses the shared punishment), `Source/Tenebral/DarkPact.cs` (shared punishment, DefOf)
- Modify: `Source/Umbral/RighteousSlaughter.cs` (`JobDriver_RighteousSlaughter.Finish`), `Source/Tenebral/TenebralPods.cs:181-188` (`OnAccepted`)
- Modify: `Languages/English/Keyed/BOR_DarkPacts.xml`

**Interfaces:**
- Consumes: `Umbral.GroupSteps`, `Umbral.StartGroup`, `Umbral.Transforming` (Task 2); `Crystallization.Fraction`, `TenebralPsychosis.BreakSettings.podThreshold`; `DarkPacts.MirrorsOn`.
- Produces:
  - `enum UmbralPactDemand { Slaughter, IntoTheDark, Offering, SeverTie, Vigil }`
  - `UmbralPacts.Active -> IEnumerable<QuestPart_UmbralPact>` (enabled parts of ongoing quests)
  - `UmbralPacts.For(Pawn umbral) -> QuestPart_UmbralPact?`
  - `UmbralPacts.Eligible(Pawn p) -> bool` (Umbral, `Umbral.Transforms`, player faction, `GroupSteps > 0`)
  - `UmbralPacts.Start(Pawn umbral, Thing mirror)`
  - `UmbralPacts.Notify_Slaughter(Pawn butcher)`, `UmbralPacts.Notify_EnteredPod(Pawn pawn)`
  - `QuestPart_UmbralPact` with `umbral`, `mirror`, `demand`, `objectiveText`, `goal`, `progress`, `target`, `TicksLeft`, `Counted`, `StatusLine`, `Succeed()`, `Fail(string reason)`
  - `DarkPacts.Punish(Thing? mirror) -> Thing?` (emits the pulse from that mirror or any home mirror; returns the source or null)
  - `QuestNode_Root_UmbralPact.Settings`, `.DurationLabel`
  - `BOR_DarkPactDefOf.BOR_UmbralPact`

- [ ] **Step 1: Shared punishment.** Move the source-picking and `MapComponent_DarkPulse.Emit` out of `QuestPart_DarkPact.Fail` into `DarkPacts.Punish(Thing? mirror)`. `QuestPart_DarkPact.Fail` calls it and keeps its own letter. Add `public static QuestScriptDef BOR_UmbralPact = null!;` to `BOR_DarkPactDefOf`.

- [ ] **Step 2: Quest script.** `UmbralPact.xml` copies `DarkPact.xml`'s shape (`autoAccept`, `NegativeEvent` letter, description as letter text, never fired at random) with root `BeyondOurReach.QuestNode_Root_UmbralPact` and fields `durationDays` 10, `slaughters` 1, `tenebrite` 150, `joints` 10, `offeringRadius` 10, `vigilWeight` 0.5.

- [ ] **Step 3: Root node.** `QuestNode_Root_UmbralPact.RunInt` reads `umbral` and `mirror` from the slate, builds the pool and rolls with `RandomElementByWeight` (Vigil `vigilWeight`, others 1):
  - Slaughter, Offering and Vigil are always in the pool.
  - IntoTheDark joins when a free colonist on a home map, other than this Umbral and not Umbral, has `Crystallization.Fraction >= podThreshold` and is not in a pod. Name one at random as `target`. Another Umbral's pact may name the same pawn.
  - SeverTie joins when the Umbral has an adult blood relative or love partner who is alive, spawned on a home map, humanlike, and a prisoner of the colony or not hostile. Same rules as `PickKin`, with the Umbral fixed as killer. Name one as `target`.
  - Set `resolvedQuestName` and `resolvedQuestDescription` from the Keyed strings in Step 6.

- [ ] **Step 4: Quest part.** `QuestPart_UmbralPact : QuestPartActivable`, modeled on `QuestPart_DarkPact`:
  - Expiry: Vigil calls `Succeed()`, every other demand calls `Fail(Reason_Expired)`.
  - `Notify_PawnKilled`:
    - Vigil fails when a humanlike dies with `umbral` as instigator.
    - SeverTie succeeds when `target` dies with `umbral` as instigator, and fails when `target` dies any other way.
    - IntoTheDark fails when `target` dies.
    - Any demand fails when `umbral` itself dies.
  - `Notify_PawnDiscarded` fails on `umbral` or `target`.
  - The long tick fails when `target` is destroyed and not dead, matching the research pact.
  - Offering, on the long tick: find a colony dark mirror with at least `tenebrite` `BOR_Tenebrite` and `joints` `BOR_ComponentIII` lying within `offeringRadius`. If found, `Succeed()` every `UmbralPacts.Active` part whose demand is Offering, then destroy exactly the offered counts (split stacks as needed). One pile pays every pact due at that moment.
  - `Succeed()`: `Umbral.StartGroup(umbral)` if alive, a positive letter naming the Umbral, and the quest ends with Success.
  - `Fail(reason)`: `DarkPacts.Punish(mirror)`, a negative letter naming the Umbral and the reason (with and without mirror, like the research pact), and the quest ends with Fail.
  - `Cleanup` nulls the references, and `ExposeData` scribes every field.
  - `QuestPartReserves` reserves `umbral` and `target`.

- [ ] **Step 5: Hooks.**
  - At the end of `JobDriver_RighteousSlaughter.Finish`, call `UmbralPacts.Notify_Slaughter(pawn)`. It counts only on the pact whose `umbral == butcher` and whose demand is Slaughter.
  - In `Building_TenebralPod.OnAccepted`, transformation branch, call `UmbralPacts.Notify_EnteredPod(pawn)`. It succeeds every active IntoTheDark part whose `target == pawn`.
  - `UmbralPacts.Start` makes the slate (`umbral`, `mirror`), generates `BOR_UmbralPact`, sends the quest letter and flashes `DarkPulse` like `CompDarkPact.StartPact`. It does nothing if `For(umbral) != null`, if `!Eligible(umbral)`, or if `Umbral.Transforming(umbral)`.

- [ ] **Step 6: Strings** in `BOR_DarkPacts.xml`, `BOR_UmbralPact_` prefix, in the existing voice. These keys are needed: `QuestName` ({0} Umbral), `QuestDescription` ({0} Umbral, {1} what the group grants, {2} price, {3} duration), one `Obj_` per demand (`Obj_Slaughter` {0} count, `Obj_IntoTheDark` {0} colonist, `Obj_Offering` {0} tenebrite count, {1} joints count, {2} radius, `Obj_SeverTie` {0} relative, {1} relation, `Obj_Vigil`), `SuccessLabel`/`SuccessText` ({0} Umbral), `FailedLabel`/`FailedText`/`FailedTextNoMirror` ({0} Umbral, {1} reason), and the reasons `Reason_UmbralDied`, `Reason_TargetDiedWrong`, `Reason_TargetLost`, `Reason_VigilBroken`. Reuse `BOR_DarkPact_Reason_Expired` and `_Abandoned`.

- [ ] **Step 7: Build and check.**
  Run: `dotnet build Source/BeyondOurReach.csproj -c Release`. Expected: `0` errors.
  The tab button comes in Task 4, so add a `Start Umbral pact` debug action to `UmbralDebug` (calls `UmbralPacts.Start(pawn, first colony mirror on the pawn's map)`) and keep it afterwards. In-game:
  - Roll until each demand has appeared. Complete Slaughter with a Righteous Slaughter, Offering with a dropped pile, IntoTheDark by loading the named colonist into a transformation pod, and SeverTie with a dev kill instigated by the Umbral.
  - Break a Vigil by having the Umbral kill.
  - Two Umbrals with Offering pacts are both paid by one pile, and it is consumed once.
  - A failed pact sends the red ring.
  - A research pact still runs alongside.

- [ ] **Step 8: Commit.**
```bash
git add Source/Umbral/UmbralPact.cs Common/Defs/QuestScriptDefs/UmbralPact.xml Source/Tenebral/DarkPactQuest.cs Source/Tenebral/DarkPact.cs Source/Umbral/RighteousSlaughter.cs Source/Tenebral/TenebralPods.cs Languages/English/Keyed/BOR_DarkPacts.xml Assemblies/BeyondOurReach.dll
git commit -m "Umbral pacts: per-Umbral quests that grant transformation groups"
```

### Task 4: Umbral pacts at the dark mirror

**Files:**
- Modify: `Source/Tenebral/DarkPact.cs` (`CompDarkPact`, `JobDriver_DarkMirrorPact`, DefOf)
- Modify: `Source/Tenebral/ITab_DarkPacts.cs`
- Modify: `Common/Defs/JobDefs/DarkPact.xml`
- Modify: `Languages/English/Keyed/BOR_DarkPacts.xml`

**Interfaces:**
- Consumes: everything `UmbralPacts` produces (Task 3), `Umbral.GroupSteps`, `Umbral.CurrentPhase`.
- Produces: `CompDarkPact.UmbralBlockReason(Pawn umbral) -> string?`; `CompDarkPact.OfferUmbral(Pawn umbral)`; `BOR_DarkPactDefOf.BOR_DarkMirrorUmbralPact` (JobDef).

- [ ] **Step 1: Communion job.** Add JobDef `BOR_DarkMirrorUmbralPact` (driver `BeyondOurReach.JobDriver_DarkMirrorUmbralPact`, report string `communing with the dark mirror.`, not casually interruptible). Make `JobDriver_DarkMirrorPact` overridable: a `protected virtual bool Invalid` (the current research checks) and a `protected virtual void Conclude()` (the current `StartPact`). Add `JobDriver_DarkMirrorUmbralPact` whose `Invalid` checks the mirror's power and `UmbralBlockReason(pawn) != null`, ignoring the communing check for itself, and whose `Conclude` calls `UmbralPacts.Start(pawn, Mirror)`. `CompDarkPact.Communing` counts both JobDefs.

- [ ] **Step 2: Block reasons.** `UmbralBlockReason`, checked in this order, returns:
  - `BOR_UmbralPact_Done` when `GroupSteps == 0`
  - `BOR_UmbralPact_Transforming` when `Umbral.Transforming`
  - `BOR_DarkPact_Communing` when someone is communing
  - `BOR_DarkPact_NeedsPower` when unpowered
  - `BOR_DarkPact_PawnDowned` when downed
  - `NoPath` when it can't reach the interaction cell
  - null otherwise

  An Umbral with a pact under way shows its pact instead of a reason. `OfferUmbral` shows a confirmation (`BOR_UmbralPact_Confirm`: {0} Umbral, {1} group label, {2} duration), then orders the Umbral itself onto the job. There is no colonist menu.

- [ ] **Step 3: Tab.**
  - **Section.** Between the active research pact box and the research list, draw an **Umbral pacts** section (header `BOR_UmbralPact_Header`). Its rows are the Umbrals on the mirror's map that are `Umbral.Transforms` and of the player faction, plus every Umbral holding an active Umbral pact wherever it is, ordered by name.
  - **Size.** Show the section only when it has rows. Up to 3 rows show at `RowHeight`, with a scroll view beyond that. The tab grows by the section's height in `UpdateSize`.
  - **Left side of a row.** `Widgets.ThingIcon` of the Umbral, its name, and a dim subline. The subline is the group label when idle (`BOR_UmbralPact_GroupParts` {0} count, {1} left; `BOR_UmbralPact_GroupExo`; `BOR_UmbralPact_GroupPod`; `BOR_UmbralPact_GroupDone`), or the pact's `StatusLine` and time left while a pact runs.
  - **Right side of a row.** **Make pact** when idle, greyed out with the reason as tooltip if `UmbralBlockReason` is set. **Abandon** during a pact, with a confirmation that calls `Fail(Reason_Abandoned)`. In dev mode a running pact adds `DEV: Complete` and `DEV: Expire` under it, and the row grows by `ButtonHeight + Gap`.
  - **Tooltip.** The objective text and the duration.
  - **Research rows.** Leave them alone: `DarkPacts.Active` only finds `QuestPart_DarkPact`.

- [ ] **Step 4: Strings.** Add the keys named in Steps 2 and 3 to `BOR_DarkPacts.xml`.

- [ ] **Step 5: Build and check.**
  Run: `dotnet build Source/BeyondOurReach.csproj -c Release`. Expected: `0` errors.
  In-game:
  - With two Umbrals and a research pact running, both Umbrals start pacts from the tab.
  - The second waits while the first communes (greyed out, "communing").
  - Rows show progress and time left.
  - Send one Umbral off in a caravan: its row stays and Abandon works.
  - A fully transformed Umbral shows "Complete" with no button.
  - The research list behaves as before.
  - Check the tab with 0, 1 and 4 Umbrals: no section, one row, scrolling.

- [ ] **Step 6: Commit.**
```bash
git add Source/Tenebral/DarkPact.cs Source/Tenebral/ITab_DarkPacts.cs Common/Defs/JobDefs/DarkPact.xml Languages/English/Keyed/BOR_DarkPacts.xml Assemblies/BeyondOurReach.dll
git commit -m "Dark mirror: Umbral pacts section in the Pacts tab"
```

### Task 5: Specs and player docs

**Files:**
- Modify: `openspec/specs/tenebral-rework/spec.md` (intro line 4, Requirement: Crystallization, implementation decisions lines 112-114)
- Modify: `openspec/specs/umbral-rework/spec.md` (intro line 4, the kill-trigger requirements and scenarios around lines 40-62, the slaughter blessing line now meaning "transform faster" for pact groups)
- Modify: `openspec/specs/dark-pacts/spec.md` (Pacts tab, One pact at a time, Non-goals; a new Requirement: Umbral pacts with the demand table and failure rules)
- Modify: `site/content/tenebral-road.md` (lines 28 and 37)
- Modify: `docs/superpowers/specs/2026-11-21-kill-to-crystallize-umbral-pacts-design.md` (Builder's calls: record the plan's pinned values: offering items and counts, separate quest class, Vigil weight 0.5, Umbral death fails the pact, Umbrals excluded from kill counting)

- [ ] **Step 1: Rewrite each section** to describe the shipped behavior in the specs' existing SHALL style, with the numbers from Global Constraints. The site page says it for players: refusing to kill leads to the Light, killing leads to the crystal, and Umbrals grow through pacts at the dark mirror.

- [ ] **Step 2: Check.** A grep for `broken too often|one kill at a time|umbral threshold|triggered by killing` over `openspec`, `site/content` and `Languages` finds nothing stale. Rebuild the site if `site/README.md` says the content is compiled (follow its command).

- [ ] **Step 3: Commit.**
```bash
git add openspec/specs docs/superpowers site
git commit -m "Docs: kill to crystallize and Umbral pacts"
```
