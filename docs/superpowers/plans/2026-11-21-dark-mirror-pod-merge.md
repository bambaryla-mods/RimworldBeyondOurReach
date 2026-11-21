# Dark Mirror Absorbs the Tenebral Pod Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task (the user runs plans Native). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The dark mirror becomes the one tier III building that hosts pacts, links to the umbral spire and works as the tenebral pod, drawn with the new animated `mirror` art. The research path becomes: every quasar project, then "dark pacts", then the mirror, then pacts, then the ultimate pact, then the spire.

**Architecture:**
- `BOR_Research_Unlocker_III` switches its class to the existing `Building_TenebralPod` and keeps its pact comp and tab.
- The pod gains a `None` mode, which it drops back to whenever nothing is pending.
- A new `CompMirrorPortal` draws the floating portal through a new dissolve shader (`BOR_MirrorPhase`) and the glow on top.
- The pact job reserves the mirror in its own reservation layer, so communing and pod use don't block each other.
- In research, `Gate_III` folds into `Analyzer_III`, and `Gate_IV` becomes a pact.

**Tech Stack:** C# (`Source/BeyondOurReach.csproj`, builds to `Assemblies/`), Harmony, RimWorld 1.7 XML defs, GLSL ShaderDefs, Keyed and DefInjected strings, Python + Pillow for the one-off icon.

**Spec:** `docs/superpowers/specs/2026-11-21-dark-mirror-pod-merge-design.md`

**Testing:** the mod has no unit test harness, so this plan can't follow red-green TDD. Each task is verified in three ways:
- A clean build: `dotnet build Source/BeyondOurReach.csproj -c Release`, with 0 errors.
- Where defs changed, a strict site build: `python site/build.py --out "$TMP/borsite" --strict`. It resolves inheritance, `MayRequire` and def names the way the game does.
- A short in-game dev-mode check listed in the task. The user runs the game; list the check for them, don't skip it.

## Global Constraints

- RimWorld 1.7 only, no per-version folders. Everything is in the `BeyondOurReach` namespace. New tenebral code goes in `Source/Tenebral/`.
- Files in different load folders must not share a relative path.
- Straight quotes only in all text. English Keyed strings only; the Polish DefInjected entries that go stale are deleted, not translated.
- Commits: plain messages, no AI attribution, no `Co-Authored-By`.
- Old saves are not supported. No back-compat code.
- Mirror def: footprint `(3,3)`, `rotatable` false, interaction cell `(0,0,-2)`, `drawSize` `(3,6)`.
- Mirror cost: Sunsteel 40, Tenebrite 200, ComponentIII 14, TenebriteAmplifier 5. WorkToBuild 33750, MaxHitPoints 787, Flammability 0, construction skill 20. Power 100000 W.
- Portal starting values: bob amplitude 0.06 cells, bob period 240 ticks, phase 60 ticks.
- Research:
  - "dark pacts" (`BOR_Research_Analyzer_III`) costs 1,000,000.
  - Pod modes are locked behind `BOR_Research_Medicine_III`.
  - `BOR_Research_Gate_IV` is a pact.
- Compat prerequisites carry `MayRequire="<mod id>"` matching the folder's `LoadFolders.xml` condition. A folder tied to several mods uses `MayRequireAnyOf="a,b"`.

## Review Focus

- **The ordered pawn never arrives** (job interrupted, pawn drafted, path blocked). The mirror must drop back to `None` and phase off, not stay lit forever. Covered: Task 3 `RefreshMode` on the rare tick, check step 5.
- **An operation is set up but nobody is in yet, and the target pawn wanders off.** The mirror stays Operating with its loaded ingredients, as today. Going to `None` must never drop ingredients. Covered: Task 3, check step 5.
- **A pact communion while someone lies in the pod, and the other way round.** Neither blocks the other. Covered: Task 4, check step 3.
- **Two mirrors on one map in different states.** Each phases on its own. Covered: Task 5 per-instance material, check step 6.
- **The Path of Light with a pawn mid-transformation in the mirror.** The pawn dies, the mirror dies, and no pact points at a dead mirror. That code is unchanged, but run it once since the building is now the mirror. Covered: Task 3 check step 5.

---

### Task 1: Research path

**Files:**
- Modify: `Common/Defs/ResearchDefs/ResearchProjectDefs.xml` (`BOR_Research_Analyzer_III` ~line 695, `BOR_Research_Gate_IV` ~919, `BOR_Research_Gate_III` ~931)
- Modify: `Common/Defs/ThingDefs_Buildings/Buildings_Research.xml:367` (spire `researchPrerequisites`)
- Modify: `Languages/English/DefInjected/ResearchProjectDef/ResearchDefs/ResearchProjectDefs.xml` (Analyzer_III ~169-176, Gate_III ~225-226, Gate_IV ~293-294, Medicine_III ~195)
- Modify: `Languages/Polish/DefInjected/` files holding `BOR_Research_Analyzer_III`, `BOR_Research_Gate_III` or `BOR_Research_Gate_IV` keys (find with grep)

**Interfaces:**
- Produces: `BOR_Research_Gate_III` no longer exists. `BOR_Research_Analyzer_III` is "dark pacts" and still the mirror's prerequisite. `BOR_Research_Gate_IV` is a pact and the spire's prerequisite.

- [ ] **Step 1: Fold Gate_III into Analyzer_III.**
  - Delete the `BOR_Research_Gate_III` def.
  - `BOR_Research_Analyzer_III` keeps parent `BOR_Research_Base_II`. It takes label `dark pacts`, Gate_III's description, `baseCost` 1000000 and `techLevel` Archotech.
  - Its prerequisites are every quasar project: every def whose parent chain reaches `BOR_Research_Base_II`, plus `BOR_Research_Analyzer_II`. Put the last project of each branch in `<prerequisites>` and the rest in `<hiddenPrerequisites>`.
  - Common projects, with no attribute: Production, Power, Battery, Armor, Weapons, Medicine, Bionics, Drugs, Joy, Storage, Hydroponics, NSynth, MoisturePump and Core_Drill `_II`; `BOR_Research_Deepdrill_II`; `BOR_Research_RangerTurret_II`; `BOR_Research_NegotiatorArtillery_II`; and the three `BOR_Quasar{Gauss,GaussSpecialist,GaussHighCaliber}Weaponry`.
  - Projects with a `MayRequire`. Each prerequisite entry copies the def's own `MayRequire` (or `MayRequireAnyOf`) when the def has one. Otherwise it uses its folder's mod id:

    | Mod id | Projects |
    |---|---|
    | `ludeon.rimworld.biotech` | `BOR_Research_PollutionPump_II` |
    | `ceteam.combatextended` | `BOR_CE_AmmoResearch_QaP`, `BOR_CE_GaussAmmo_II_Ni`, `BOR_CE_BeamAmmo_II`, and the Common `BOR_QuasarBeam{,Specialist,Heavy}Weaponry` (declared `MayRequire="ceteam.combatextended"`, `ResearchProjectDefs.xml:541,556,571`) |
    | `fox.simplelearning` | `BOR_QuasarDesk` |
    | `hobbes.terrapumps` | `BOR_Research_TerraWet_II`, `BOR_Research_TerraSoil_II`, `BOR_Research_TerraDeep_II` |
    | `onofre150.me.vaccines` | `BOR_Research_Vaccines_II`, `BOR_Research_Vaccines_Combined_II` |
    | `smashphil.vehicleframework` | `BOR_Research_KwazaariTruck` |

  - Before writing, re-run the scan so nothing is missed: every `<ResearchProjectDef` under `Common` and `Mods` whose `ParentName` is `BOR_Research_Base_II` or `MechtechQuasarBase`. Note each def's own `MayRequire`.
  - Keep Analyzer_III's `requiredAnalyzed` (`BOR_DiluteTenebriteShard`, `BOR_TenebriteMotor`) and its discovery letter. Reword the def's own `discoveredLetterTitle` and `discoveredLetterText` as well as the DefInjected ones (step 4).
  - Tree: "dark pacts" stays at Analyzer_III's `researchViewX` 14. `BOR_Research_NegotiatorArtillery_II` (x 19, inside the tier III columns) moves into the quasar columns at x 13 or lower. Tier III and IV stay where they are.

- [ ] **Step 2: Gate_IV becomes the ultimate pact.**
  - Change its parent to `BOR_Research_Base_III_Pact`. Remove `baseCost`, so the pact parent's 1E+15 applies.
  - Its prerequisites are every other tier III pact: `BOR_Research_{Production,Medicine,Bionics,PacifierTurret,Core_Drill,MoisturePump}_III`, plus these compat pacts:

    | Mod id | Pacts |
    |---|---|
    | `adaptive.storage.framework` | `BOR_Research_Storage_III` |
    | `ludeon.rimworld.biotech` | `BOR_Research_PollutionPump_III` |
    | `ceteam.combatextended` | `BOR_CE_AmmoResearch_TC` |
    | `fox.simplelearning` | `BOR_TenebralDesk` |
    | `hobbes.terrapumps` | `BOR_Research_Terra{Wet,Soil,Deep}_III` |
    | `onofre150.me.vaccines` | `BOR_Research_Vaccines_III`, `BOR_Research_Vaccines_Combined` |

  - Use the same scan as step 1, with parent `BOR_Research_Base_III_Pact` or `MechtechTenebralBase`, and the same `MayRequire` rule.
  - The Mechtech projects are left out of both gates. They have `requiresMechanitor`, so including them would lock a Biotech colony with no mechanitor out of tenebral and umbral tech.
  - Description: `The ultimate pact. The Dark Archotechs show how to raise an umbral spire, where umbral research begins.`

- [ ] **Step 3: Spire.** Change the spire's (`BOR_Research_Unlocker_IV`) `researchPrerequisites` from `BOR_Research_Gate_III` to `BOR_Research_Gate_IV`.

- [ ] **Step 4: Text.**
  - English DefInjected:
    - Delete the Gate_III keys.
    - Analyzer_III takes the new label and description.
    - Its `discoveredLetterText` opens with the Archotechs' whispers teaching tenebrite and the mirror, and keeps the "pacts, not study" paragraph.
    - Gate_IV takes the new description.
    - Medicine_III's text says it unlocks the dark mirror's pod modes instead of building the tenebral pod.
  - Polish, in `Languages/Polish/DefInjected/ResearchProjectDef/BOR_1.6_Expanded.xml`:
    - delete all four Analyzer_III keys (label, description, letter title, letter text);
    - delete both Gate_III keys;
    - delete Medicine_III's description, keeping its label.

    Gate_IV has no Polish keys.

- [ ] **Step 5: Verify.**
  - The strict site build passes. It does not catch unknown prerequisites (`site/gen/extract.py:329-331` humanizes them silently), so it isn't enough on its own.
  - Grep `BOR_Research_Gate_III` over `Common`, `Mods`, `Languages`, `Source`, `site/content`, `openspec`. Expected: no hits except openspec text that Task 6 fixes.
  - For each prerequisite name in the new lists, grep that a def with that `defName` exists.
  - In game, load twice, once with only Core + BOR and once with the compat mods you have. In both, the log has no "Could not resolve cross-reference" naming a research project.
  - In the research tab, "dark pacts" sits after every quasar project and the mirror is locked behind it. With dev "finish all research" up to tier III, the umbral research gate shows as a pact in the mirror's Pacts tab.

- [ ] **Step 6: Commit.** `git add` the changed files by path; never `git add -A` or `commit -a`, because unrelated `Mods/Biotech/Defs/tier*hediffs.xml` edits sit in the working tree. Then `git commit -m "Research: dark pacts is the last quasar project, the umbral gate is the ultimate pact"`.

---

### Task 2: The mirror def, textures and icon

**Files:**
- Modify: `Common/Defs/ThingDefs_Buildings/Buildings_Research.xml` (`BOR_Research_Unlocker_III`, ~244-352)
- Modify: `Common/Defs/ThingDefs_Buildings/tier3/Buildings_Tenebral.xml` (delete `BOR_TenebralCasketBase` and `BOR_TenebralPod_III`, fix the header comment)
- Modify: `Mods/Biotech/Patches/TenebralGeneForge.xml`
- Create: `Textures/BOR/Things/Building/tier3/mirror/dark_mirror_icon.png`
- Move: `Textures/BOR/Things/Building/tier3/research_unlocker/` to `UnusedTextures/BOR/Things/Building/tier3/research_unlocker/`
- Delete: `Textures/BOR/Things/Building/tier3/TenebralPod/`

**Interfaces:**
- Produces: `BOR_Research_Unlocker_III` with `thingClass` `BeyondOurReach.Building_TenebralPod`. Task 5 adds the `CompProperties_MirrorPortal` entry to its comps.

- [ ] **Step 1: Icon.** Using Pillow, alpha-composite `big-red-button.png`, then `umbrussy-off.png`, onto a transparent 384×768 canvas. Crop to the alpha bounding box and save as `dark_mirror_icon.png` in the same folder. Run the script from the scratchpad, not the repo.

- [ ] **Step 2: Mirror def.**
  - Set `thingClass` to `BeyondOurReach.Building_TenebralPod`.
  - `graphicData`: `texPath` `BOR/Things/Building/tier3/mirror/big-red-button`, `Graphic_Single`, `drawRotated` false, `drawSize` `(3,6)`, `drawOffset` `(0,4,1.5)`. y 4 matches the old mirror, the altar and the spire, so the tall art draws over pawns standing north of it. Keep `shadowData` out.
  - `uiIconPath` `BOR/Things/Building/tier3/mirror/dark_mirror_icon`.
  - `size` `(3,3)`, `rotatable` false, `interactionCellOffset` `(0,0,-2)`.
  - `drawerType` `MapMeshAndRealTime`, `tickerType` Normal, `containedPawnsSelectable` true, `blockWind` true, `canOverlapZones` false.
  - `<building>` keeps `destroySound` and adds `isPlayerEjectable` true and `ai_chillDestination` false.
  - `inspectorTabs`: `BeyondOurReach.ITab_DarkPacts`, `ITab_ContentsCasket`.
  - Cost and stats as in Global Constraints.
  - Remove `minifiedDef`, `thingCategories`, `Mass`, `CompAffectedByFacilities`, `CompProperties_ReportWorkSpeed` and the mirror's own `DefModExtension_CountsAsResearchBench`.
  - Add `PlaceWorker_PreventInteractionSpotOverlap` next to `PlaceWorker_ShowFacilitiesConnections`.
  - Keep `CompDarkPact`, power, flickable, `CompFacility`, the UI scheme, `DefModExtension_TenebralBuilding`, the `BOR_Research_Analyzer_III` prerequisite and the dropdown.
  - Leave the description for Task 6.

- [ ] **Step 3: Delete the pod defs.** Delete `BOR_TenebralCasketBase` and `BOR_TenebralPod_III`. Change the file's header comment to cover only the altar.

- [ ] **Step 4: Biotech patch.** Make it a `PatchOperationAdd` on `/Defs/ThingDef[defName="BOR_Research_Unlocker_III"]/inspectorTabs`, with value `<li>BeyondOurReach.ITab_GeneForge</li>`. Comment: `Gene Forge tab on the dark mirror.`

- [ ] **Step 5: Textures.** `git mv` the tier3 `research_unlocker` folder into `UnusedTextures/BOR/Things/Building/tier3/`, and `git rm -r` the `TenebralPod` folder. Leave tier1 and tier2 `research_unlocker.png` alone.

- [ ] **Step 6: Verify.**
  - The strict site build passes.
  - A grep for `BOR_TenebralPod_III|TenebralCasketBase|tier3/research_unlocker|tier3/TenebralPod` over `Common`, `Mods`, `Source` and `site/content` finds nothing.
  - The C# build has 0 errors.
  - In game:
    - The mirror places as 3×3, showing the button base and the composed icon.
    - The pod gizmo opens the pod window, and the Pacts tab works.
    - The tab row shows Pacts, Contents and (with Biotech and a sleeper) Gene Forge, and the Contents tab is readable in the Tenebral colours.
    - With a colonist in stasis, dev-trigger the Path of Light: the mirror becomes a 3×3 husk and the colonist is ejected.

- [ ] **Step 7: Commit.** `git add` the whole `Textures/BOR/Things/Building/tier3/mirror/` folder (it is untracked today, so the def would otherwise point at missing art), plus the changed defs, the patch and the texture moves, by path. Then `git commit -m "Dark mirror is the tenebral pod: merged def, new textures, old pod removed"`.

---

### Task 3: Mode-less pod and the Medicine III lock

**Files:**
- Modify: `Source/Tenebral/TenebralPods.cs` (enum ~57-63, `CanAcceptFor` ~111, `Order` ~142-179, `Tick` ~338, `GetGizmos` ~405, `EjectContents` ~455, `GetInspectString` ~489, `ExposeData` ~518)
- Modify: `Source/Tenebral/TenebralPodWindow.cs` (constructor ~59)
- Modify: `Source/Tenebral/TenebralPsychosis.cs` (`BOR_TenebralDefOf` ~64)
- Modify: `Languages/English/Keyed/BOR_TenebralUmbral.xml`

**Interfaces:**
- Produces:
  - `TenebralPodMode.None` (first value).
  - `Building_TenebralPod.InMode -> bool` (`mode != None`).
  - `Building_TenebralPod.PodUnlocked -> bool`.
  - Task 5 reads `InMode` and `Occupant`.

- [ ] **Step 1: Enum and default.** Add `None` as the first `TenebralPodMode` value. The `mode` field defaults to `None`, and `Scribe_Values.Look(ref mode, "podMode", TenebralPodMode.None)`. Add `public bool InMode => mode != TenebralPodMode.None;`.

- [ ] **Step 2: Lock.**
  - Add `public static ResearchProjectDef BOR_Research_Medicine_III = null!;` to `BOR_TenebralDefOf`.
  - Add `public static bool PodUnlocked => BOR_TenebralDefOf.BOR_Research_Medicine_III.IsFinished;` on `Building_TenebralPod`.
  - `CanAcceptFor` first returns the new `"BOR_TenebralPodLocked".Translate(BOR_TenebralDefOf.BOR_Research_Medicine_III.LabelCap)` when locked. It then returns the new `BOR_TenebralPodNoMode` when `forMode == None`.
  - The pod gizmo is disabled with `BOR_TenebralPodLocked` when locked. The `Transforming` disable stays.

- [ ] **Step 3: Falling back to None.** Add `private void RefreshMode()`. It sets `mode = TenebralPodMode.None` directly, never through `SetMode`, when all of these hold:
  - `InMode`;
  - `!HasAnyContents`;
  - `operation == null`;
  - `!Transforming`;
  - `!PawnOnTheWay()`.

  `private bool PawnOnTheWay()` returns false when `!Spawned`. Otherwise it is true when any pawn in `Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer)` has, in its current job or its `jobs.jobQueue`, either:
  - an `EnterCryptosleepCasket` job with target A == this; or
  - a `CarryToCryptosleepCasket` job with target B == this.

  `TryTakeOrderedJob` enqueues, so the queue check is needed.

  Where to call `RefreshMode()`:
  - In `RareStep()`, right after `CheckGeneForge()`, before both early returns. (`RareStep` returns early when nobody is inside, which is exactly the case this is for.)
  - In `EjectContents()`, on every path. Today it returns early for Stasis (`TenebralPods.cs:463-466`), so restructure it: skip the sickness removal for Stasis instead of returning, then call `RefreshMode()` last.

- [ ] **Step 4: Order, window and inspect.**
  - `Order` checks before changing anything:
    - when the patient walks, `patient.CanReserve(this)`;
    - when carried, `carrier.CanReserve(patient) && carrier.CanReserve(this)`, with the carrier search filtering on these.

    On failure it returns `"BOR_PodOrderRefused".Translate(<that pawn>.LabelShortCap)` and leaves the mode, the operation and the ingredients untouched. Only then does it call `SetMode`, `StartOperation` and `TryTakeOrderedJob` as today. If `TryTakeOrderedJob` still returns false, it calls `RefreshMode()` and returns the same reason.
  - The window constructor sets `action = pod.InMode ? pod.Mode : TenebralPodMode.Stasis`. `Actions` never contains `None`.
  - `GetInspectString` writes the mode line only when `InMode`.
  - `BorIcons.Pod` already falls back to the stasis icon for `None`; leave it.

- [ ] **Step 5: Keys, build and check.**
  - Add these keys:
    - `BOR_TenebralPodNoMode`: `choose a mode in the mirror's pod controls first`
    - `BOR_PodOrderRefused`: `{0} can't take the order right now.`
    - `BOR_TenebralPodLocked`: `Needs {0}.`
  - Build: 0 errors.
  - In game, dev mode:
    - While pawn A walks into the mirror in Healing, order pawn B into Stasis. The window gives a clean refusal, there is no red log line, and the mirror stays Healing for A.
    - Save and reload with a pawn on the way, and again with an operation waiting for its patient. Both stay in their mode after loading.
    - Before Medicine_III the gizmo is greyed out with the reason. After it, a fresh mirror shows no mode line, and right-click "enter" is refused with the no-mode reason.
    - Order a colonist into stasis, then draft them on the way. Within about 4 seconds the mirror is mode-less again.
    - Set up an operation with no one inside, let the ingredients load, and cancel the target's job. It stays Operating with its ingredients.
    - Heal a tenebral pawn until the pod opens. It is mode-less afterwards.
    - With a colonist transforming, trigger the Path of Light (`OrientisDebug`). The pawn and the mirror die, and the Pacts tab on another mirror (if any) still works.

- [ ] **Step 6: Commit.** `git commit -m "Tenebral pod: mode-less until ordered, locked behind tenebral medicine"`

---

### Task 4: Pacts and the pod share the mirror

**Files:**
- Create: `Common/Defs/Misc/BOR_ReservationLayers.xml`
- Modify: `Source/Tenebral/DarkPact.cs` (`BOR_DarkPactDefOf` ~17, `JobDriver_DarkMirrorPact.TryMakePreToilReservations` ~306)
- Modify: `Source/Buildings/ResearchBenchSubstitutes.cs` (research tab bench requirement for pacts)

**Interfaces:**
- Produces: `ReservationLayerDef BOR_DarkMirrorCommune`, available as `BOR_DarkPactDefOf.BOR_DarkMirrorCommune`.

- [ ] **Step 1: Layer.** Define `<ReservationLayerDef><defName>BOR_DarkMirrorCommune</defName></ReservationLayerDef>`, with a comment saying that pact communion reserves the mirror on its own layer so it never blocks the pod's casket jobs. Add the DefOf field.

- [ ] **Step 2: Reserve on it.** `TryMakePreToilReservations` reserves with `layer: BOR_DarkPactDefOf.BOR_DarkMirrorCommune`. `JobDriver_DarkMirrorUmbralPact` inherits this. Nothing on the pact-ordering path (`DarkPact.cs:176`, `:258`) calls `CanReserve` on the mirror, so nothing else changes.

- [ ] **Step 3: Research tab.**
  - Pact projects still inherit `requiredResearchBuilding` = the mirror, which is no longer a `Building_ResearchBench`. The vanilla tab would therefore show that requirement as unmet.
  - Add a Harmony postfix in `ResearchBenchSubstitutes.cs` on `ResearchProjectDef.PlayerHasAnyAppropriateResearchBench` (getter). For projects with `DefModExtension_DarkPact`, set `__result` to whether any colony map has a spawned player building that `DarkPacts.IsMirror` accepts.
  - Check: the research tab shows a pact project's mirror requirement as met once a mirror stands.

- [ ] **Step 4: Build and check.**
  - Build: 0 errors.
  - In game, each case uses a live casket job; a pawn already lying inside holds no reservation, so it doesn't test the layer.
    - While a communion runs, order a colonist into the pod. They walk past and climb in.
    - While a colonist walks in (or waits through the enter delay), start a pact communion. It starts.
    - While a communion runs, order a prisoner in. A carrier brings them.

- [ ] **Step 5: Commit.** `git commit -m "Dark mirror: pact communion reserves its own layer, so the pod stays usable"`

---

### Task 5: The animated portal

**Files:**
- Create: `Common/Shaders/BOR_MirrorPhase.frag.glsl`
- Modify: `Common/Defs/ShaderDefs/BOR_Shaders.xml`
- Create: `Source/Tenebral/MirrorPortal.cs`
- Modify: `Common/Defs/ThingDefs_Buildings/Buildings_Research.xml` (mirror comps)

**Interfaces:**
- Consumes: `Building_TenebralPod.InMode`, `Building_TenebralPod.Occupant` (Task 3).
- Produces: `CompProperties_MirrorPortal`, `CompMirrorPortal`, ShaderDef `BOR_MirrorPhase`.

- [ ] **Step 1: Shader.**
  - Same layout as `BOR_BubbleMask.frag.glsl`. Uniforms: `_Color`, `_MainTex_ST`, `_Phase` (x phase 0..1, y edge width, z noise scale), `_EdgeColor`. Samplers: `_MainTex` (off) and `_OnTex` (on).
  - Both textures sample at `vUv * _MainTex_ST.zw + _MainTex_ST.xy`.
  - `n` is value noise from an integer hash of `floor(vUv * _Phase.z)`, bilinearly smoothed, in [0,1].
  - With `p = phase * (1 + w)`:
    - `n < p - w` shows `on`;
    - `n < p` shows `on` with its rgb lerped toward `_EdgeColor` (alpha = max of both textures' alpha);
    - anything else shows `off`.
  - That gives pure off at phase 0 and pure on at phase 1. Discard when alpha <= 0.001.
  - ShaderDef `BOR_MirrorPhase`: vertex `BOR_BubbleMask.vert.glsl`, transparent, no depth write, render queue 2960, and Clamp wrap overrides for `_MainTex` and `_OnTex`.

- [ ] **Step 2: Props.** `CompProperties_MirrorPortal : CompProperties` with these fields:
  - `offTexPath`, `onTexPath`, `glowTexPath`;
  - `float bobAmplitude = 0.06f`, `int bobPeriodTicks = 240`, `int phaseTicks = 60`;
  - `float edgeWidth = 0.08f`, `float noiseScale = 24f`, `Color edgeColor = (1, 0.25, 0.2, 1)`;
  - `int glowPulseTicks = 240`, `float glowPulseStrength = 0.5f`.

  Each field gets a one-line comment.

- [ ] **Step 3: Comp.** `CompMirrorPortal : ThingComp`, on a `Building_TenebralPod` parent.
  - **Material.**
    - Create its own `Material` lazily, with the shader, `mainTexture` off and `_OnTex` on.
    - Destroy it in `PostDeSpawn` with `UnityEngine.Object.Destroy`.
  - **Phase.**
    - The comp keeps `bool lastInMode`, `float phaseAtChange` and `int changeTick`. None of them is saved.
    - On the first draw after spawn, `phaseAtChange` = target, so the phase snaps to its target.
    - When `InMode` differs from `lastInMode`, it stores the current phase and the tick.
    - `phase = clamp01(phaseAtChange ± (now − changeTick) / phaseTicks)`.
  - **Drawing, in `PostDraw`.**
    - Base matrix: center = `parent.DrawPos + parent.Graphic.DrawOffset(parent.Rotation)`, scale = draw size. The bob adds `bobAmplitude × sin(2π × ticks / bobPeriodTicks)` to z.
    - Portal: height +0.012, queue 2960, `_Phase` set on the comp's own material.
    - Glow: only while `InMode && Occupant != null`. `MaterialPool.MatFrom(glowTexPath, ShaderDatabase.MoteGlow)` faded by the pulse as in `CompGlowOverlay`, with the same bob, height +0.013, queue 2961.
    - Use game ticks throughout, so everything freezes on pause.

- [ ] **Step 4: Wire it.** Add the comp to the mirror. Paths: `BOR/Things/Building/tier3/mirror/umbrussy-off`, `.../umbrussy-on`, `.../aroused`.

- [ ] **Step 5: Build.** 0 errors. The C# build doesn't compile GLSL, so it says nothing about the shader.

- [ ] **Step 6: Check in game.**
  - First, the log has no `BOR_MirrorPhase` config error and no "not registered ... using engine default" warning. Without that, a missing dissolve looks like a tuning problem.
  - The portal floats and bobs over the base, and the base bottom sits on the footprint's bottom edge. Tune `drawOffset` if not.
  - Ordering a pawn in phases the portal on over about 1 second, with a red dissolve edge, and the noise stays fixed as it bobs.
  - The glow pulses once they're inside.
  - Eject them and it phases off.
  - Build two mirrors and activate only one: only that one phases.
  - Pause: the bob and the phase freeze.

- [ ] **Step 7: Commit.** `git commit -m "Dark mirror: floating portal that phases on with the pod, glow while occupied"`

---

### Task 6: Text, site and specs

**Files:**
- Modify: `Common/Defs/ThingDefs_Buildings/Buildings_Research.xml` (mirror description; spire description if it mentions research at the mirror)
- Modify: `Languages/English/DefInjected/ThingDef/ThingDefs_Buildings/Buildings_Research.xml:10`
- Modify: `Languages/Polish/DefInjected/ThingDef/BOR_1.6_Expanded.xml:84` (delete the stale mirror description)
- Modify: `Languages/English/Keyed/BOR_TenebralUmbral.xml` (`BOR_TenebralPodTitle`, `BOR_TenebralPodModeDesc`, `BOR_TenebralPodInstalled`, `BOR_PodNobodyToCarry`, `BOR_TenebralPodChooseDesc`, and any other string reading "the pod" as its own building)
- Modify: `Source/Tenebral/TenebralGeneForge.cs:300` (stale comment), `Source/Tenebral/TenebralPodWindow.cs:10` and `TenebralPods.cs` class comments
- Modify: `site/content/tenebral-road.md:19-21,37`, `site/content/orientis.md:27`, `site/content/research.md`
- Modify: `openspec/specs/tenebral-rework/spec.md`, `openspec/specs/orientis-faction/spec.md:45,84-87,140`, `openspec/specs/orientis-faction/test-steps.md`, `openspec/specs/ui-theme/spec.md`, `openspec/specs/dark-pacts/spec.md`, `openspec/specs/artifact-rework/spec.md`, `openspec/specs/component-harvest/spec.md`. Change them wherever they name the tenebral pod, Gate_III or research at the mirror; run the Step 4 grep first to find the exact places.
- Modify: `Scripts/data.csv:62`, `Scripts/promo.csv:62` (texture path column to `tier3/mirror/dark_mirror_icon`)

- [ ] **Step 1: Mirror description.** `A dark mirror for speaking with the Dark Archotechs. Pacts are made here, and an umbral spire needs one linked to it. It is also a tenebral pod that works four ways: stasis freezes whoever lies inside; healing closes the wounds of tenebral and Umbral pawns ten times faster than nature would; an operation installs a tenebral organ or appendage in one step; and transformation holds a pawn whose body is at least half crystallized for the rest of a year before they emerge as an Umbral. The pod needs tenebral medicine.` Put it in the def and in the English DefInjected entry, and delete the Polish one.

- [ ] **Step 2: Keyed and comments.** Reword the listed keys so they name the dark mirror (`BOR_TenebralPodTitle` becomes `Dark mirror`; `BOR_TenebralPodInstalled` becomes `The dark mirror installed {1} in {0}.`). Fix the class comments. In `ITab_GeneForge`, delete the `Scheme` override and its comment (`TenebralGeneForge.cs:300-302`). The mirror carries the Tenebral scheme now, so the fallback is dead.

- [ ] **Step 3: Site and specs.**
  - Site and openspec describe one building: pacts, the spire link, and the four pod modes behind tenebral medicine. They describe the research path as: all quasar research, then dark pacts, then the mirror, then pacts, then the ultimate pact (the umbral research gate), then the spire.
  - `research.md` stops calling the mirror a research bench.

- [ ] **Step 4: Verify.**
  - The strict site build passes.
  - A grep for `tenebral pod|TenebralPod_III|Gate_III|galactic analyzer` (case-insensitive) over `Languages/English`, `site/content`, `openspec/specs`, `Common` finds nothing stale. `Building_TenebralPod` in C# and the class comment are fine.

- [ ] **Step 5: Commit.** `git commit -m "Docs and text: the dark mirror is the pod, dark pacts close the quasar tier"`
