# BOR expedition sites

Sites for the Expeditions mod (`bambaryla.expeditions`) that only make sense with BOR, plus BOR's data for Expeditions' generic sites. Everything lives in a new `Mods/Expeditions` folder that loads only when Expeditions is active. It uses the hooks described in `Expeditions/openspec/expeditions-mod-hooks-design.md` (called "the hooks doc" below), and can't be built before them.

## Intent
Expeditions lets a colony send a squad off-map and read a report when it comes back. BOR is a climb through tiers, factions and, at the top, a choice of path. These sites make expeditions part of that climb:

- **A second way up.** The key artifacts that open tier II and tier III research (the data drive; the diluted tenebrite shard and the tenebrite motor) come from the Strange Signals chain today. Two sites give a riskier alternative. Strange Signals stays as it is. The user: "it should be an alternative, not the only way." The user judged the trade fair: Signals costs waiting and its consequences, the expedition costs a squad's time and risk.
- **Something to do at each tier,** gated so tier loot never reaches a colony below that tier.
- **Each path gets its own sites.** Path of Light colonies are crusaders who purge the dark. Path of Dark colonies reap and pay pacts.

## Goals
- Nine BOR-only sites (below), each gated on its tier's research or the colony's path.
- BOR data for Expeditions' generic sites and faction overrides: the Solar Republic pays in its goods, the Badzaari and the Vanquishers hold their own loot, the courier can carry a data drive, dormant mechs can be tier mechs, and the Kwazaari pay a bounty in scrip.
- A new hediff, **touch of the holy**: a permanent blessing.
- Light colonies never receive dark items from an expedition.
- BOR still loads and plays the same without Expeditions.

## Non-goals
- Schism sites (nemesis camp, champion's stronghold). They are noted in `openspec/specs/schism/spec.md` and wait for the Schism.
- A Kwazaari escort bought with scrip, forge-hunting runs, a debt contract, and raids for ignoring a staging camp. All considered and dropped.
- Changes to Strange Signals or to dark pacts themselves.
- Separate BOR copies of sites that Expeditions already has. The Solar contract, the Badzaari den and the staging camp became faction overrides on `Exp_FriendlyJob` and `Exp_HostileOutpost`.

## Anti-goals
- A colony below a tier getting that tier's goods from an expedition.
- A key artifact dropping after it has been analysed.
- A Path of Light colony receiving tenebrite, tenebral or Umbral items.
- A dark pact paid twice, or paid by an expedition after it has ended.
- BOR's main assembly referencing Expeditions.

## Approaches
1. **BOR's `Mods/Expeditions` with generic hooks in Expeditions (chosen).** BOR's sites, overrides and workers live next to the code they call. Expeditions only gains generic hooks. This is how BOR already handles six other mods.
2. **Expeditions' `Compat/BOR`.** Everything in one repo, but Expeditions would track BOR internals.
3. **XML only, reflection into BOR.** No second assembly, but fragile.

The user picked 1, with the research gate and faction targeting in Expeditions itself.

## Layout
- `LoadFolders.xml`: `<li IfModActive="bambaryla.expeditions">Mods/Expeditions</li>`.
- `Mods/Expeditions/Defs/`: the site defs, `ExpeditionFactionDef`s and the touch hediff.
- `Mods/Expeditions/Patches/`: loot entries and mech kinds added to Expeditions' generic sites.
- `Mods/Expeditions/Source/` → `Mods/Expeditions/Assemblies/BORExpeditions.dll`. It references `BeyondOurReach.dll` and `Expeditions.dll` (not copied), and holds the gates, effects and loot filter below.
- Translations in `Languages/*/DefInjected` and `Keyed`, with `BOR_` keys.

## Gates (BOR's `SiteGate`s)
| Gate | Open when |
| --- | --- |
| `SiteGate_Research` (Expeditions built-in) | research finished |
| `BeyondOurReach.SiteGate_Path` with `path` (None, Light or Dark) and `not` (bool) | the colony's `ColonyPath` matches, or doesn't when `not` is set |
| `BeyondOurReach.SiteGate_PactObjective` with `objectives` | `DarkPacts.Active` is non-null (it only returns a pact whose part is still `Enabled`, so a paid or failed pact closes the gate) and its `objective` is in the list. `DeadlineTick` is the pact's end: now + `TicksLeft` |

BOR's Expeditions classes use the `BeyondOurReach` namespace, like the rest of BOR. Pact sites set `immediate` (hooks doc, section 1), so they are on the board the moment a pact opens and expire with it.

Tier gates use the analyzer research of each tier:
- **Tier I:** `BOR_Research_Analyzer_I`
- **Tier II:** `BOR_Research_Analyzer_II`
- **Tier III:** `BOR_Research_Analyzer_III`
- **Umbral:** `BOR_Research_Gate_IV`

## Effects (BOR's workers)
- **`PawnEffect_TenebriteExposure`** (`emission`, default 3): calls `TenebralPsychosis.UpdatePawn(pawn, site.daysOnSite, emission)` once. Ordinary pawns come back with latent psychosis that fades at home by the usual decay. Pawns with tenebral implants or gear start from their floor and can tip into psychosis. Umbral pawns get the resistance factor. Orientis pawns never reach these sites (gates).
- **`PawnEffect_TouchOfTheHoly`** (`chance`): Success only, Orientis pawns only (`Orientis.IsOrientis`), and only pawns without a touch already. On the roll, it picks a random body part from those the blessing bench offers. There is no static list: `CompBlessingBench.BlessableParts(Pawn)` is an instance method on a spawned bench. The effect reads `CompProperties_BlessingBench.blessableParts` off the bench's ThingDef instead, and resolves it against the pawn the same way. If that logic is worth sharing, the builder moves it to a static helper in BOR. It removes a `BOR_Blessing` on that part if there is one and adds `BOR_TouchOfTheHoly`. Report line: "{pawn} was touched by the holy ({part})."
- **`PawnEffect_Reaping`** (`kills`, an IntRange, rolled per pawn):
  - Every pawn with the bloodlust need (`Need_Murder`) has the need refilled.
  - Only pawns for whom `TenebralPsychosis.CountsKill` holds also get the rolled kills added to `Hediff_TenebralBreaks.kills` through `TenebralPsychosis.BreakCounter`. That's the same rule a kill at home follows.
  - Crystallization and its ability rolls follow from the new count by BOR's existing checks. The builder makes sure the counter's normal update runs, so a jump of several kills is handled.
  - Kills are capped so one trip can't take a pawn to a lethal brain, and they also count toward an open `KillPeople` pact. See *Reaping: decided after review*.
- **`OutcomeEffect_MeditationPower`** (`amount`), in `OnArrival`: Success gives the full amount, Partial half. It goes into the caches of the map the squad is set down on, through `MapComponent_MeditationPower.Add`. `Add` returns void and silently drops what doesn't fit, so the effect reads `Stored` before and after to report how much was banked. With no map (they became a caravan) or no cache, the report says the power faded.
- **`OutcomeEffect_Scrip`** (`amount`, a FloatRange, and `perThing`, a list of thing def and scrip pairs), in `OnArrival`: it calls `GameComponent_Kwazaari.Instance.AddScrip(float)` (`Source/Kwazaari/KwazaariLedger.cs`). `perThing` counts matching items that actually came home, after the haul cut and anything lost on the road. Partial halves the `amount` part.
- **`OutcomeEffect_PactProgress`**, in `Apply` (the deed is done at the site, so a squad lost on the road still paid): it adds to the active pact's progress. Success pays the whole remaining count, Partial pays half of what is left, rounded up. Progress is counted by a private `Count()` on `QuestPart_DarkPact`, so BOR adds a public entry point, something like `AddProgress(int)`, which calls the existing `Succeed()` once the goal is met. If the pact ended while the squad was away, nothing happens and the report says the mirror no longer cares.
- **`BORLootFilter`** (an `ExpeditionLootFilter`): on the Path of Light, it strikes every item `TenebralPsychosis.MadeOfTenebrite` matches, plus tenebrite itself and Umbral items. The builder picks the exact test, reusing what "Light and dark stay apart" uses. Report line: "The dark things they found were burned where they lay."

## Touch of the holy
- `BOR_TouchOfTheHoly`: the same stages and effects as `BOR_Blessing`, with no `HediffCompProperties_Disappears`. It is visible, and not removable by surgery.
- One per pawn, ever. The effect checks for any existing touch.
- The blessing bench (`Blessing.cs`) treats a part with a touch as unavailable: it's shown disabled with the reason "touched by the holy". This is a change in BOR's main assembly, and it reads the hediff by def name so it works without Expeditions too.
- What happens to a touch if a pawn later stops being Orientis isn't settled. It can't happen today, since the paths are permanent.

## Sites
Challenge entries are (weight, requirement). Risk names are Expeditions' families. Every number is a starting point for tuning. Def names are `BOR_Exp_*`.

### The second way up
| Site | Gates | Challenges | Loot | Risk |
| --- | --- | --- | --- | --- |
| Tenebral ruins | Research `BOR_Research_Analyzer_II`; Path `Light`, not | Search salvage (2, 120), Hack (1, 130), Endure (1, 120), Fight (1, 200) | Always: Plasteel 20~50, ComponentSpacer 1~2, BOR_ComponentII 1~2 (0.5). Full success, `untilAnalyzed`, chance 0.5 each: BOR_DiluteTenebriteShard 1, BOR_TenebriteMotor 1. From tier III on (`research` BOR_Research_Analyzer_III): BOR_Tenebrite 10~30, BOR_ComponentIII 1~3 (0.5) | harsh, no captors |

The site is the remains of a Tenebral Vanquisher structure. Tenebrite is made, not mined, so it's ruins and not a vein. Pawn effect: exposure, emission 3. `daysOnSite` 1.5. No terrain requirement.

The data drive needs no BOR site. It goes on Expeditions' `Exp_DownedCourier` (see *Data for generic sites*).

### Tier sites
| Site | Gates | Challenges | Loot | Risk |
| --- | --- | --- | --- | --- |
| Umbral hunting ground | Research `BOR_Research_Gate_IV`; Path `Light`, not; commonness 0.3 | Fight (2, 350), Endure (1.5, 150) | BOR_TenebriteAmplifier 1, full success, chance 0.2; Umbral gear 1 (the builder picks the pool from the tier IV/V items), chance 0.3 | harsh, no captors |
| Failing neutronium store | Research: whichever project unlocks neutronium storage (the builder finds it) | Haul (2, 250), Endure (1, 120) | BOR_Neutronium 20~60 | its own: Success unharmed 0.7, injured 0.25, killed 0.05; Partial 0.4, 0.45, 0.15; Failure 0.1, 0.5, 0.4; never captured |

The hunting ground also gets exposure, emission 1. For the neutronium store, the builder checks whether loose neutronium decays or explodes off a powered tank. If it does, the haul needs to survive the trip home, or the site description needs to warn that it won't.

### Kwazaari
| Site | Gates | Challenges | Rewards | Risk |
| --- | --- | --- | --- | --- |
| Tech recovery | Research `BOR_Research_Analyzer_II` | Search salvage (2, 120), Hack (1.5, 130) | No items. `OutcomeEffect_Scrip` 20~40 | default |

The Kwazaari are hidden, so they're never a patron in Expeditions' sense. Scrip is paid by the effect.

### Path of Light
| Site | Gates | Challenges | Rewards | Risk |
| --- | --- | --- | --- | --- |
| Crusade | Path `Light`; `settlementLink`: hostile, `factionDefs` BOR_Tenebrites, Destroy on Success, Nothing on Partial, retaliation from the site's faction on Partial | Fight (2.5, from the settlement, `fightMin` 400), Endure (1, 150) | No loot. Joiners (chance 0.5, 1~3, Colonist, `partialFactor` 0.5) as freed captives. `OutcomeEffect_GoodwillAll` 8, `includeHostile`, except BOR_Tenebrites and BOR_Umbrals, Success only. Touch, chance 0.25 | harsh (the Vanquishers take prisoners) |
| Holy retreat | Path `Light` | Teach (2, 100), Debate contest (1, 100) | `OutcomeEffect_MeditationPower` 600. Touch, chance 0.05 | gentle |
| Umbral hunt | Path `Light`; commonness 0.3 | Fight (2, 350), Endure (1.5, 150) | Touch, chance 0.15. A commented-out effect that lowers the Schism's Hunger meter, with the note "Umbrals don't raid outside named events; enable once the Schism exists" | harsh, no captors |

A crusade win razes the settlement, and if it was the Vanquishers' last, the faction is defeated. The user wanted this. The Vanquishers' goods are all tenebral, and crusaders can't touch them, so the crusade has no loot table. A Partial leaves the settlement standing and the Vanquishers raid back.

### Path of Dark
| Site | Gates | Challenges | Rewards | Risk |
| --- | --- | --- | --- | --- |
| Reaping | Path `Dark`; `needsFaction` Hostile | Fight (2, 300), Stealth (0.5, 100) | `PawnEffect_Reaping` with kills 2~5, on Success and Partial. Loot: WeaponsRanged 1~2 (0.5), Apparel 1~2 (0.5) | harsh |
| Pact: raze the settlement | Pact objective `DestroySettlement`; `settlementLink` on that pact's `QuestPart_DarkPact.settlement`, `need` None (the owner may turn hostile mid-pact), Destroy on Success, Nothing on Partial, `affectRelations` (the owner turns hostile on any outcome) | Fight (2, from the settlement), Stealth (1, 120) | The pact's own completion fires when the settlement is gone. No loot | harsh |
| Pact: slaughter | Pact objective `KillPeople` | Fight (2, 300) | `OutcomeEffect_PactProgress` | harsh |
| Pact: the great hunt | Pact objective `SlaughterAnimals` | Search game (1.5, 100), Fight (1.5, 200) | `OutcomeEffect_PactProgress`. Loot: the tile's meat and leather, as `Exp_GameHerd` rolls them | no captors |

For the pact sites:
- The offer's deadline is the pact's time left.
- The settlement version is a BOR subclass of `SettlementLinkWorker`:
  - `CanOffer` matches only the pact's settlement, not any friendly one.
  - `StillWanted` (hooks doc, section 4) turns the squad back if the pact has ended, so nobody razes a town for a pact that already failed.
- Reaping and an open `KillPeople` pact: see *Reaping: decided after review*.
- The other pact objectives (execution, kinslayer, burning, betrayal, organs, ossuary) need the colony itself and get no site.

## Data for generic sites
`ExpeditionFactionDef`s and patches in `Mods/Expeditions`:

| Target | What BOR adds | Gate |
| --- | --- | --- |
| BOR_SolarRepublic as patron, any site | `payment`: BOR_Sunsteel, BOR_Sunweave, BOR_FuelRod | `BOR_Research_Analyzer_I` |
| BOR_BadzaarHazaard holding `Exp_HostileOutpost` | `siteLoot`, added: BOR_Gun_Badzaar_* (1~2, a category or a list), BOR_QuasarAlloy 10~30, BOR_ComponentII 1~3 (0.5) | `BOR_Research_Analyzer_II` |
| BOR_Tenebrites holding `Exp_HostileOutpost` | `siteLoot`, added: BOR_Tenebrite 5~15, BOR_ComponentIII 1~2 (0.4) | `BOR_Research_Analyzer_III` |
| BOR_BadzaarHazaard as the slavers of `Exp_SlaverConvoy` | `siteLoot`, added: badzaar guns 1 (0.5) | `BOR_Research_Analyzer_II` |
| BOR_BadzaarHazaard holding `Exp_SettlementBounty` | `siteLoot`, added: badzaar guns 1~3. `outcomeEffects`: `OutcomeEffect_Scrip` with `perThing` 3 per badzaar gun brought home. The squad keeps the guns, so the player chooses between selling and using them | `BOR_Research_Analyzer_II` |
| `Exp_DownedCourier` (patch) | loot: BOR_DataDrive 1, full success, `untilAnalyzed`, `research` BOR_Research_Production_I, chance 0.35; BOR_CircuitBoard 1~3, `research` BOR_Research_Production_I | — |
| `Exp_DormantMechs` (patch, Biotech) | `mechKinds` entries: BOR_Mech_SolarLifter and BOR_Mech_SolarCleaner with `research` BOR_Research_Analyzer_I; BOR_Mech_QuasarDriller and BOR_Mech_QuasarFabricor with `research` BOR_Research_Analyzer_II | per entry |

On a Light colony, the loot filter burns the Vanquisher outpost's tenebrite. It also catches a colony that chooses Light while a squad is walking home, because the filter runs again at arrival (hooks doc, section 7). BOR's own Path of Light burn skips items inside containers, and the expedition holder is one.

## What is left to the builder
- The exact research for the neutronium store, the Umbral gear pool, and the list of badzaar guns.
- The exact "dark item" test for the loot filter.
- Where BOR's existing code needs a public entry point (kill counting, pact progress) instead of the expedition reaching into fields.
- Player site (`site/`) entries for the new content.

## Reaping: decided after review
- **It counts toward the pact.** The user confirmed that a reaping's kills count toward an open `KillPeople` pact. The reaping pawn effect adds the sum of the kills it rolled, across the squad, to the pact directly. It uses the same public progress entry point as `OutcomeEffect_PactProgress`.
- **Kills are capped.** The kills to the pod threshold are `max(1, 1000 − 100 × implants)`, so without a cap a pawn with 10 or more tenebral parts could reach a lethal brain from one batch. The user chose to cap it. A pawn's added kills are cut so its crystallization after the trip stays below the point where the brain kills. The cut kills still count toward the pact, since the people died either way.

## Open questions
- What a touch of the holy does if a pawn could ever leave the Orientis xenotype. It can't today.
