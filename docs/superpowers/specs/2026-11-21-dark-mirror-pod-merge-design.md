# Dark mirror absorbs the tenebral pod

Status: awaiting approval. Nothing here is built.

## Intent

The dark mirror and the tenebral pod become one building with new art. The mirror stops being a research bench, because tenebral research is unlocked by dark pacts, not researched. It hosts pacts, it is the facility the umbral spire links to, and it is now also the pod with all four modes. The new art (`Textures/BOR/Things/Building/tier3/mirror/`) is animated: a portal floats over a button base, phases on when the pod is given a job and glows while someone is inside.

## Goals

- One building, `BOR_Research_Unlocker_III` (label "dark mirror"), that hosts dark pacts, links to the umbral spire as a facility, and works as the tenebral pod: stasis, healing, operating, transformation, and the Biotech gene forge.
- The pod is mode-less until the player picks a mode and a pawn in the pod window.
- Animated look built from the four `mirror` textures.
- The old pod and mirror textures are cleaned up.

## Non-goals

- Old saves. Saved pods vanish and saved mirrors may misbehave; the user accepts this. No back-compat code.
- Changing what any pod mode or pact does, beyond the mode-less state below.
- Research at the mirror, including tier I/II and hi-tech research through its `CountsAsResearchBench` today. All of that goes; everything at the mirror is a pact.

## Anti-goals

- A plain texture swap between portal off and on. The change must read as one form phasing into another.
- The pact tab or the spire link breaking because the building's class changed.
- Any research project left with no bench that can run it.

## Approaches (phase-in effect)

| Approach | Trade-off | |
|---|---|---|
| A. Alpha crossfade with the vanilla transparent shader | No new shader. Since off and on share their outline, it reads as the interior brightening, close to a swap. | Rejected |
| B. New dissolve shader: on appears over off through a noise threshold with a red edge | About 40 lines of GLSL plus a ShaderDef, modelled on `BOR_BubbleMask`. The only option that reads as phasing. | **Chosen** |
| C. Reuse `BOR_BubbleMask`: on grows from the portal centre inside a mask | Reuses existing code but needs a mask texture, and reads as "filling", not phasing. | Rejected |

The bob needs no shader: it is a sine offset applied when drawing. The bubble shader is not reused.

## Design

### Research

The story the tree tells (user's): you finish every quasar project and hit a wall. Then you find a way to commune with the Dark Archotechs. Their whispers teach you tenebrite and the dark mirror, and from then on you deal with them through pacts. The ultimate pact unlocks the umbral spire.

- **Analyzer_III and Gate_III become one project.** Today they're two projects that both mean "dark pacts". `BOR_Research_Gate_III` is deleted. `BOR_Research_Analyzer_III` keeps its defName, because the mirror, Production_III, MoisturePump_III and the tenebrite letter already point at it.
  - It takes the label "dark pacts" and Gate_III's description.
  - It stays tier II, researched at the quasar bench.
  - It is the last quasar project: it requires every tier II project (everything with parent `BOR_Research_Base_II`, plus Analyzer_II).
    - That includes the compat folders: Biotech, CE, SimpleLearning, TerraPumps, Vaccines, VehicleFramework and any others found.
    - Each compat entry gets a `MayRequire` attribute that matches its folder's condition in `LoadFolders.xml`. A folder tied to several mods gets `MayRequireAnyOf="mod.a,mod.b"`. (User's decision.) Today every compat folder holding a tier II project depends on a single mod, so only plain `MayRequire` is needed.
    - The last ones in each branch go in `prerequisites`, the rest in `hiddenPrerequisites`. Builder's split.
    - The Biotech Mechtech projects are left out of this gate and out of the ultimate pact, because they need a mechanitor. (User's decision.)
  - Pact projects keep the mirror as `requiredResearchBuilding`. A Harmony postfix on `PlayerHasAnyAppropriateResearchBench` reports it met while a mirror stands, so the research tab doesn't show an unmet bench.
  - Cost 1,000,000 (Gate_III's). *My call.*
  - Its "forming tenebrite" discovery letter stays and is reworded as the Archotechs' whispers.
  - It moves to the end of the quasar column in the research tree view.
- **`BOR_Research_Gate_IV` becomes the ultimate pact.** Its parent changes to `BOR_Research_Base_III_Pact`, so it has infinite cost and is unlocked in the mirror's Pacts tab. It requires every other tier III pact. *My call: all of them, not only the old hidden three.* The spire's research prerequisite changes from Gate_III to Gate_IV. The tier IV projects that require Gate_IV stay as they are and are researched at the spire.
- Gate_III's English DefInjected keys are deleted; Analyzer_III's and Gate_IV's are rewritten, with stale Polish entries removed. Site research pages are updated to match.
- The other `BOR_Research_Base_III` projects are pact research already. Their inherited `requiredResearchBuilding` stays; it is never used, because their cost (1E+15) means they are never researched.
- `BOR_Research_Base_IV`/`V` keep requiring the mirror as a linked facility of the spire. That only needs `CompFacility` on the mirror.
- Pod modes stay locked until `BOR_Research_Medicine_III` is finished, as the pod was. The mirror itself still unlocks at `BOR_Research_Analyzer_III`, so pacts come first. Until then, the pod gizmo is disabled with a keyed reason naming the research, and `CanAccept` refuses. (User's decision.)

### Defs

- `BOR_TenebralPod_III` and the abstract `BOR_TenebralCasketBase` are deleted. The mirror copies the casket fields it needs: `containedPawnsSelectable`, `tickerType` Normal, `blockWind`, `canOverlapZones` false, and the pod's `<building>` block with `isPlayerEjectable` true, `ai_chillDestination` false and the destroy sound. `BOR_Research_Unlocker_III` keeps its defName, so the facility requirements and the spire's linkables still point at it.
- `thingClass` is `BeyondOurReach.Building_TenebralPod`. `drawerType` is `MapMeshAndRealTime`: the base is static and the portal is drawn every frame.
- Kept from the mirror: `CompDarkPact`, `ITab_DarkPacts`, `CompPowerTrader` (100 kW), `CompFlickable`, `CompFacility` (spire link and research speed offset), `PlaceWorker_ShowFacilitiesConnections`, the Tenebral UI scheme, `DefModExtension_TenebralBuilding`, the research prerequisite `BOR_Research_Analyzer_III`, the `BOR_Unlockers` dropdown and construction skill 20. The pod's `PlaceWorker_PreventInteractionSpotOverlap` is added.
- Removed from the mirror: the `Building_ResearchBench` class, `CompAffectedByFacilities`, `CompProperties_ReportWorkSpeed`, the mirror's own `DefModExtension_CountsAsResearchBench` (the spire's stays), `minifiedDef`, `thingCategories` and `Mass`. A casket with a pawn inside must not be minified, and vanilla flags non-minifiable buildings that have thing categories.
- Inspector tabs: `ITab_DarkPacts` and `ITab_ContentsCasket`. The Biotech patch `Mods/Biotech/Patches/TenebralGeneForge.xml` changes to a `PatchOperationAdd` on `/Defs/ThingDef[defName="BOR_Research_Unlocker_III"]/inspectorTabs` that adds the `<li>`, with its comment updated.
- Footprint 3×3, `rotatable` false, interaction cell `(0,0,-2)`, the centre cell just south of the footprint. An odd width keeps the cell centred under the portal; with 2×2 it would sit under the left column. Graphic: `BOR/Things/Building/tier3/mirror/big-red-button`, `Graphic_Single`, `drawRotated` false, draw size (3,6), with a draw offset that puts the bottom of the base on the bottom edge of the footprint, tuned in game. *My call.*
- `uiIconPath` points to a new composed icon texture (base + umbrussy-off) in the `mirror` folder. The build menu, the Pacts tab row (`ITab_DarkPacts.cs:310`) and the site then show the whole mirror, not just the button. Blueprints and the Path of Light husk (`DarkBuildings.cs:182`, which draws `original.graphic`) show only the base; that's acceptable. *My call.*
- Cost: the mirror's cost plus the pod's: Sunsteel 40, Tenebrite 200, ComponentIII 14, TenebriteAmplifier 5. Work to build is the mirror's (33750). HP is the mirror's (787) and flammability is the pod's (0). *My call.*

### Mode-less state

- `TenebralPodMode` gains `None` as its first value. It is the default for a fresh mirror and the `Scribe_Values` default.
- **In a mode** means the player chose a mode and a pawn in the pod window (`Order`). The mirror stays in that mode while any of these holds:
  - a pawn is inside;
  - a pawn is on its way, meaning some spawned pawn of the player faction (colonist, slave or carrier) has a current job of entering or carrying a pawn to this mirror;
  - an operation is set (Operating mode keeps the chosen recipe, target and loaded ingredients for the target, as today);
  - a transformation is running.
- Otherwise it drops back to `None`. The check runs on the existing rare tick, plus at once on eject. In `EjectContents` the drop comes after the Stasis cryptosleep-sickness check. The drop sets the field directly, not through `SetMode`, which would cancel the operation and drop ingredients.
- In `None`, `CanAccept` refuses with a new keyed reason ("Choose a mode in the pod window first"). That blocks the vanilla "enter" float menu. *My call.*
- In `None`:
  - The pod window opens with Stasis preselected. Its `Actions` array never contains `None`.
  - The pod gizmo shows the stasis icon.
  - The inspect string leaves out the mode line.
  
  *My call.*
- `Order` checks the result of `TryTakeOrderedJob` and returns a reason instead of setting the mode when the job is refused.
- Pod modes keep working without power, as the pod did. Power gates only pacts, as today. *My call.*
- The gene forge needs Stasis with someone inside, so it is unaffected.

### Pacts and the pod on one building

- They share the interaction cell and the building's reservation:
  - the pact job, `JobDriver_DarkMirrorPact` (`DarkPact.cs:306`);
  - the gene forge job;
  - vanilla enter and carry-to-casket.
- They may coexist. A pawn communing at the cell does not stop someone lying inside.
- The builder makes the pact job and the enter/carry jobs reserve without blocking each other: each takes its own reservation layer or skips the building reservation when the other holds it. If that doesn't work cleanly, the fallback is mutual exclusion with a keyed block reason on both sides ("Someone is communing at the mirror" / "The mirror is in use"). Builder's call between the two.
- The Path of Light kills the mirror (`DestroyWithOccupant`), not just husks it. Pacts already handle a lost mirror (`DarkPact.cs:86-89`, `DarkPactQuest.cs:222`). Nothing to add.

### Drawing

A new comp on the mirror (`CompMirrorPortal`, in `Source/Tenebral/`) draws the portal and the glow in `PostDraw`. Both use the building's draw size and sit over the base. The glow is in this comp, not in `CompGlowOverlay`, which is sealed and finds its source only among comps.

1. **Portal.** `umbrussy-off` and `umbrussy-on` through the new dissolve shader, offset vertically by `amplitude × sin(2π × ticks / period)`. Starting values: amplitude 0.06 cells, period 240 ticks. It uses game ticks, so it freezes on pause.
2. **Phase.** The comp records the tick and direction of the last change between `None` and a mode. Phase is `clamp((now − changedTick) / phaseTicks)`, with phaseTicks about 60, run forward or backward. That needs no `CompTick`, and nothing is saved, so after a load it starts settled.
   - The shader shows on where noise is below the phase and off elsewhere, with a thin bright red edge at the threshold.
   - The noise is a hash in the shader, in texture UV space, so the pattern stays put on the portal as it bobs.
   - Each mirror has its own material, created on spawn and destroyed on despawn (or a `MaterialPropertyBlock`), because the phase differs per building. The bubble overlay's one shared material per def cannot do this.
3. **Glow.** `aroused` with `MoteGlow`, pulsing like `CompGlowOverlay`, with the same bob offset. It draws above the portal (a higher render queue or height than the portal) and only while the mode is not `None` and a pawn is inside.

The drawing follows the mode, not power. A powered-off mirror still phases on for the pod, because the pod modes don't need power. *My call.*

The comp's properties hold the texture paths, amplitude, period and phase ticks, so they can be tuned in XML.

### Shader

`Common/Shaders/BOR_MirrorPhase.frag.glsl`, plus a ShaderDef in `Common/Defs/ShaderDefs/BOR_Shaders.xml` that reuses `BOR_BubbleMask.vert.glsl`. Uniforms: `_MainTex` (off), `_OnTex` (on) and `_Phase` (phase and edge width). Transparent, no depth write, render queue 2960, Clamp wrap overrides for both `_MainTex` and `_OnTex`. The UV handling follows `BOR_BubbleMask`: `_MainTex_ST` flips both textures the same way, which is safe because all four mirror textures are 384×768.

### Textures

- Delete `Textures/BOR/Things/Building/tier3/TenebralPod/`.
- Move `Textures/BOR/Things/Building/tier3/research_unlocker/` to `UnusedTextures/BOR/Things/Building/tier3/research_unlocker/`. The tier1 and tier2 `research_unlocker.png` stay: those benches still use them.
- Add the composed menu icon (see Defs).

### Text

- Mirror description (Common def and English DefInjected `Buildings_Research.xml:10`): rewritten to cover pacts, the spire link and the four modes. The Polish DefInjected mirror description (`BOR_1.6_Expanded.xml:84`) is removed so it falls back to English, not left stale.
- Research texts: Medicine_III's "Build the tenebral pod" (English DefInjected `ResearchProjectDefs.xml:195`) becomes unlocking the mirror's pod modes. Analyzer_III's "greatly aids your research" (`Common/Defs/ResearchDefs/ResearchProjectDefs.xml:701`) is reworded.
- Keyed strings the player now reads on the mirror and that say "pod" (`BOR_TenebralPodTitle`, `BOR_TenebralPodInstalled`, `BOR_TenebralPodModeDesc` and the like, `BOR_TenebralUmbral.xml`) are reworded where "pod" no longer reads right. New keys: the `None` refusal, the Medicine_III lock reason and, if mutual exclusion is chosen, the two block reasons.
- Stale comments: `Buildings_Tenebral.xml:3`, `TenebralGeneForge.cs:300` ("the pod has no scheme of its own").

### Docs and data

- Player site:
  - `site/content/tenebral-road.md`, `orientis.md:27` and any other page naming the tenebral pod now describe the mirror.
  - Rebuild per `site/README.md`.
- openspec: update the places that name the pod as its own building:
  - `tenebral-rework`
  - `umbral-rework`
  - `orientis-faction` (`spec.md:45`, `:84-87`, `:140`)
  - `dark-pacts`, if it describes the mirror's build
- `Scripts/data.csv:62` and `Scripts/promo.csv:62` point at the old `research_unlocker` texture; repoint them at the new icon.

## Left to the builder

- The draw offset, the bob values, the edge width and colour, and the glow pulse values, tuned in game.
- Reservation coexistence or mutual exclusion between pacts and the pod.
- Keyed string wording.
- Pawns north of the mirror draw over the portal, as with the spire. This is accepted.
- The split between visible and hidden prerequisites for "dark pacts", and the tree view positions.
