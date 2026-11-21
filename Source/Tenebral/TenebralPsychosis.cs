using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace BeyondOurReach;

// Tunables for the psychosis hediff; placed on BOR_TenebralPsychosis.
public class DefModExtension_TenebralPsychosis : DefModExtension
{
    // Severity floor each tenebral implant holds the pawn at.
    public float floorPerImplant = 0.1f;

    // Tenebrite weapons and armor work like a bionic at this ratio of an implant.
    public float gearImplantRatio = 0.5f;

    // Severity gained per day per unit of nearby tenebrite emission.
    public float exposureGainPerDay = 0.03f;

    // Severity lost per day while above the floor and unexposed.
    public float decayPerDay = 0.05f;

    // Severity gained per day while below the floor.
    public float riseToFloorPerDay = 0.25f;

    // Radius in which tenebrite buildings and floors emit.
    public float emissionRadius = 6f;

    // Emission weight of one building and of one floor cell; the total is capped.
    public float buildingEmission = 1f;
    public float floorEmission = 0.1f;
    public float maxEmission = 3f;

    // Growth multiplier for pawns carrying the psychosis resistance gene.
    public float resistantFactor = 0.25f;
}

// Tunables for the hidden break and kill counter; placed on BOR_TenebralBreakCounter.
public class DefModExtension_TenebralBreaks : DefModExtension
{
    public int orientisThreshold = 3;

    // Kills as a tenebral it takes a pawn with no tenebral parts to crystallize the brain up to podThreshold. Every
    // counted kill crystallizes, from the first.
    public int killsToPodThreshold = 1000;

    // Each tenebral part on the pawn takes this many kills off killsToPodThreshold, down to a single kill. The rate
    // is worked out again at every kill, so a new part speeds up what is left. Crystallization has no cap: past
    // podThreshold it keeps growing at the same rate, and a fully crystallized brain kills.
    public int killsSavedPerPart = 100;

    // A dark ability roll each time crystallization passes a multiple of this.
    public float abilityRollEvery = 0.1f;

    // Brain crystallization needed before a Tenebral pod in transformation mode accepts the pawn.
    public float podThreshold = 0.5f;
}

[DefOf]
public static class BOR_TenebralDefOf
{
    public static ThingDef BOR_Tenebrite = null!;
    public static ThingDef BOR_ComponentIII = null!;
    public static ThingDef BOR_Femtoclusters = null!;
    public static ThingDef BOR_TenebralOrgan = null!;
    public static ThingDef BOR_TenebralAppendage = null!;

    public static HediffDef BOR_TenebralPsychosis = null!;
    public static HediffDef BOR_TenebralBreakCounter = null!;
    public static HediffDef BOR_Crystallized = null!;

    public static StatDef BOR_TenebralAltarPiety = null!;

    public static JobDef BOR_AltarUnlock = null!;
    public static JobDef BOR_AltarProduce = null!;
    public static JobDef BOR_LoadTenebralPod = null!;

    public static ResearchProjectDef BOR_Research_Medicine_III = null!;

    [MayRequireBiotech]
    public static JobDef BOR_GeneForge = null!;

    [MayRequireBiotech]
    public static HediffDef BOR_GeneForgeExtracted = null!;

    static BOR_TenebralDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(BOR_TenebralDefOf));
    }
}

public static class TenebralPsychosis
{
    public static DefModExtension_TenebralPsychosis Settings =>
        BOR_TenebralDefOf.BOR_TenebralPsychosis.GetModExtension<DefModExtension_TenebralPsychosis>() ?? new DefModExtension_TenebralPsychosis();

    public static DefModExtension_TenebralBreaks BreakSettings =>
        BOR_TenebralDefOf.BOR_TenebralBreakCounter.GetModExtension<DefModExtension_TenebralBreaks>() ?? new DefModExtension_TenebralBreaks();

    // A pawn is psychotic once the psychosis hediff has grown past its hidden latent stage.
    public static bool IsPsychotic(Pawn pawn)
    {
        Hediff? hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(BOR_TenebralDefOf.BOR_TenebralPsychosis);
        return hediff != null && hediff.Visible;
    }

    // The pawn's hidden break and kill counter, added the first time it is needed.
    public static Hediff_TenebralBreaks BreakCounter(Pawn pawn)
    {
        if (pawn.health.hediffSet.GetFirstHediffOfDef(BOR_TenebralDefOf.BOR_TenebralBreakCounter) is not Hediff_TenebralBreaks counter)
        {
            counter = (Hediff_TenebralBreaks)HediffMaker.MakeHediff(BOR_TenebralDefOf.BOR_TenebralBreakCounter, pawn);
            pawn.health.AddHediff(counter);
        }
        return counter;
    }

    // A kill that counts toward crystallization: by a tenebral colonist (psychotic, with at least one tenebral implant)
    // who is not already Umbral.
    public static bool CountsKill(Pawn killer) =>
        killer.RaceProps.Humanlike && killer.Faction == Faction.OfPlayer && !Umbral.IsUmbral(killer) && IsPsychotic(killer)
        && TenebralMurder.ImplantCount(killer) > 0;

    // Severity at which the hediff stops being latent and the pawn counts as psychotic.
    public static float PsychoticSeverity
    {
        get
        {
            foreach (HediffStage stage in BOR_TenebralDefOf.BOR_TenebralPsychosis.stages)
            {
                if (stage.becomeVisible)
                {
                    return stage.minSeverity;
                }
            }
            return 0f;
        }
    }

    public static float Severity(Pawn pawn) =>
        pawn.health?.hediffSet?.GetFirstHediffOfDef(BOR_TenebralDefOf.BOR_TenebralPsychosis)?.Severity ?? 0f;

    public static HediffComp_PsychosisLimit? LimitComp(Pawn pawn) =>
        pawn.health?.hediffSet?.GetFirstHediffOfDef(BOR_TenebralDefOf.BOR_TenebralPsychosis)?.TryGetComp<HediffComp_PsychosisLimit>();

    // Whether psychosis has reached the limit the player set for this pawn.
    public static bool AtWorkLimit(Pawn pawn) => LimitComp(pawn)?.Reached ?? false;

    // The work the limit applies to: bills, research, the altar and the forges.
    public static bool IsLimitedWork(WorkGiver giver) => giver is WorkGiver_DoBill or WorkGiver_Researcher or WorkGiver_DarkAltar or WorkGiver_ForgeBase;

    // Ends limited work at a tenebrite building once the pawn is at the limit. Severity only moves in the exposure check, which calls this.
    public static void StopWorkAtLimit(Pawn pawn)
    {
        Job? job = pawn.CurJob;
        if (job?.workGiverDef == null || !IsLimitedWork(job.workGiverDef.Worker) || job.targetA.Thing is not Thing building
            || !MadeOfTenebrite(building) || !AtWorkLimit(pawn))
        {
            return;
        }
        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        if (pawn.Faction == Faction.OfPlayer)
        {
            Messages.Message("BOR_PsychosisLimitStopped".Translate(pawn.LabelShortCap, building.Label), pawn, MessageTypeDefOf.NeutralEvent);
        }
    }

    public static int Breaks(Pawn pawn) =>
        (pawn.health?.hediffSet?.GetFirstHediffOfDef(BOR_TenebralDefOf.BOR_TenebralBreakCounter) as Hediff_TenebralBreaks)?.breaks ?? 0;

    // Everything made with tenebrite: stuff, or tenebrite in the cost list.
    public static bool MadeOfTenebrite(Thing thing) =>
        thing.Stuff == BOR_TenebralDefOf.BOR_Tenebrite || TenebriteDefs.Contains(thing.def);

    private static HashSet<ThingDef>? tenebriteDefs;
    private static HashSet<TerrainDef>? tenebriteTerrain;

    private static HashSet<ThingDef> TenebriteDefs => tenebriteDefs ??= DefDatabase<ThingDef>.AllDefs
        .Where(d => d.costList != null && d.costList.Any(c => c.thingDef == BOR_TenebralDefOf.BOR_Tenebrite))
        .ToHashSet();

    private static HashSet<TerrainDef> TenebriteTerrain => tenebriteTerrain ??= DefDatabase<TerrainDef>.AllDefs
        .Where(d => d.costList != null && d.costList.Any(c => c.thingDef == BOR_TenebralDefOf.BOR_Tenebrite))
        .ToHashSet();

    // Worn or held tenebrite gear, counted as implants at the gear ratio.
    public static float GearImplants(Pawn pawn)
    {
        int count = 0;
        if (pawn.apparel != null)
        {
            foreach (Apparel apparel in pawn.apparel.WornApparel)
            {
                if (MadeOfTenebrite(apparel))
                {
                    count++;
                }
            }
        }
        if (pawn.equipment != null)
        {
            foreach (ThingWithComps eq in pawn.equipment.AllEquipmentListForReading)
            {
                if (MadeOfTenebrite(eq))
                {
                    count++;
                }
            }
        }
        return count * Settings.gearImplantRatio;
    }

    // Emission from tenebrite buildings, walls and floors around the pawn, like pollution.
    public static float Emission(Pawn pawn)
    {
        if (!pawn.Spawned)
        {
            return 0f;
        }
        DefModExtension_TenebralPsychosis s = Settings;
        Map map = pawn.Map;
        // An Umbral shield replaces every other source in its range. Orientis pawns only suffer its mood penalty.
        if (CompUmbralShield.Covers(map, pawn.Position))
        {
            return Orientis.IsOrientis(pawn) ? 0f : s.maxEmission;
        }
        float total = 0f;
        int cells = GenRadial.NumCellsInRadius(s.emissionRadius);
        for (int i = 0; i < cells; i++)
        {
            IntVec3 c = pawn.Position + GenRadial.RadialPattern[i];
            if (!c.InBounds(map))
            {
                continue;
            }
            if (TenebriteTerrain.Contains(c.GetTerrain(map)))
            {
                total += s.floorEmission;
            }
            List<Thing> things = map.thingGrid.ThingsListAtFast(c);
            for (int j = 0; j < things.Count; j++)
            {
                Thing t = things[j];
                // Multi-cell buildings count once, at their origin.
                if (t.def.category == ThingCategory.Building && t.Position == c && MadeOfTenebrite(t))
                {
                    total += s.buildingEmission;
                }
            }
        }
        return Mathf.Min(total, s.maxEmission);
    }

    public static float ResistanceFactor(Pawn pawn) =>
        Umbral.HasPsychosisResistance(pawn) ? Settings.resistantFactor : 1f;

    // Moves the pawn's psychosis by one exposure check worth `days`.
    public static void UpdatePawn(Pawn pawn, float days, float emission)
    {
        DefModExtension_TenebralPsychosis s = Settings;
        float resist = ResistanceFactor(pawn);
        float floor = Mathf.Min(0.95f, (TenebralMurder.ImplantCount(pawn) + GearImplants(pawn)) * s.floorPerImplant) * resist;
        HediffSet set = pawn.health.hediffSet;
        Hediff? hediff = set.GetFirstHediffOfDef(BOR_TenebralDefOf.BOR_TenebralPsychosis);
        if (hediff == null)
        {
            if (floor <= 0f && emission <= 0f)
            {
                return;
            }
            hediff = HediffMaker.MakeHediff(BOR_TenebralDefOf.BOR_TenebralPsychosis, pawn);
            hediff.Severity = 0.001f;
            pawn.health.AddHediff(hediff);
        }
        float sev = hediff.Severity;
        if (emission > 0f)
        {
            sev += emission * s.exposureGainPerDay * days * resist;
        }
        if (sev < floor)
        {
            sev = Mathf.Min(floor, sev + s.riseToFloorPerDay * days);
        }
        else if (emission <= 0f && sev > floor)
        {
            sev = Mathf.Max(floor, sev - s.decayPerDay * days);
        }
        // A hediff carrying a player-set limit stays, hidden, so the limit survives the psychosis fading.
        if (sev <= 0.001f && floor <= 0f && emission <= 0f && !(hediff.TryGetComp<HediffComp_PsychosisLimit>()?.LimitSet ?? false))
        {
            pawn.health.RemoveHediff(hediff);
            return;
        }
        hediff.Severity = Mathf.Min(1f, sev);
    }
}

// Runs the tenebrite exposure checks for every humanlike on the map, a few pawns per tick.
public class MapComponent_TenebralExposure : MapComponent
{
    private const int Interval = 2000;

    private readonly Dictionary<Pawn, float> lastEmission = new();

    public MapComponent_TenebralExposure(Map map) : base(map)
    {
    }

    public float LastEmission(Pawn pawn) => lastEmission.TryGetValue(pawn, out float e) ? e : 0f;

    public static float EmissionFor(Pawn pawn) =>
        pawn.MapHeld?.GetComponent<MapComponent_TenebralExposure>()?.LastEmission(pawn) ?? 0f;

    public override void MapComponentTick()
    {
        int tick = Find.TickManager.TicksGame;
        // Not AllHumanlikeSpawned, which rebuilds a shared list on every read.
        IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn pawn = pawns[i];
            if ((tick + pawn.thingIDNumber) % Interval != 0 || pawn.Dead || !pawn.RaceProps.Humanlike)
            {
                continue;
            }
            float emission = TenebralPsychosis.Emission(pawn);
            lastEmission[pawn] = emission;
            TenebralPsychosis.UpdatePawn(pawn, (float)Interval / GenDate.TicksPerDay, emission);
            TenebralPsychosis.StopWorkAtLimit(pawn);
        }
        if (tick % GenDate.TicksPerDay == 0)
        {
            lastEmission.RemoveAll(kv => kv.Key.Destroyed || kv.Key.MapHeld != map);
        }
    }
}

// Hidden counter of berserk breaks while psychotic and kills as a tenebral. Breaks lead to the Orientis choice; every
// kill crystallizes the brain further.
public class Hediff_TenebralBreaks : HediffWithComps
{
    private const int ChoiceCheckInterval = 60;

    public int breaks;
    public int kills;

    // The Orientis choice waits for the break that earned it to end.
    private bool choicePending;

    public override bool Visible => false;

    // Brain crystallization one kill adds for this pawn: podThreshold spread over the kills its tenebral parts leave.
    public static float SeverityPerKill(Pawn pawn)
    {
        DefModExtension_TenebralBreaks s = TenebralPsychosis.BreakSettings;
        int kills = Mathf.Max(1, s.killsToPodThreshold - s.killsSavedPerPart * TenebralMurder.ImplantCount(pawn));
        return s.podThreshold / kills;
    }

    public override void TickInterval(int delta)
    {
        base.TickInterval(delta);
        if (!choicePending || !pawn.IsHashIntervalTick(ChoiceCheckInterval, delta) || pawn.InMentalState)
        {
            return;
        }
        choicePending = false;
        if (pawn.Faction == Faction.OfPlayer)
        {
            GameComponent_OrientisPath.Instance.OfferChoice(pawn);
        }
    }

    public void Notify_Break()
    {
        breaks++;
        if (breaks >= TenebralPsychosis.BreakSettings.orientisThreshold)
        {
            choicePending = true;
        }
    }

    public void Notify_Kill()
    {
        kills++;
        bool started = Crystallization.Grow(pawn, SeverityPerKill(pawn));
        if (started && pawn.Faction == Faction.OfPlayer)
        {
            Find.LetterStack.ReceiveLetter("BOR_CrystallizationLabel".Translate(pawn.LabelShortCap),
                "BOR_CrystallizationText".Translate(pawn.Named("PAWN")), LetterDefOf.ThreatSmall, pawn);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref breaks, "breaks");
        Scribe_Values.Look(ref kills, "kills");
        Scribe_Values.Look(ref choicePending, "choicePending");
    }
}

// Brain crystallization. Its severity is how far the brain has crystallized, and it takes that much off the brain's
// efficiency: 1% crystallized is 1% less brain, so a fully crystallized brain kills.
public class Hediff_Crystallized : HediffWithComps
{
    private HediffStage? stage;
    private float stageSeverity = -1f;

    // With no def stages the vanilla setter never sees a stage change, so it never refreshes capacities. This does,
    // for every change, dev tools included.
    public override float Severity
    {
        get => base.Severity;
        set
        {
            float old = base.Severity;
            base.Severity = value;
            if (base.Severity != old && pawn?.health?.hediffSet?.hediffs.Contains(this) == true)
            {
                pawn.health.Notify_HediffChanged(this);
            }
        }
    }

    public override HediffStage CurStage
    {
        get
        {
            if (stage == null || stageSeverity != Severity)
            {
                stageSeverity = Severity;
                stage = new HediffStage { partEfficiencyOffset = -Severity };
            }
            return stage;
        }
    }

    // One decimal: a kill can add well under a percent.
    public override string SeverityLabel => Severity.ToStringPercent("0.0");
}

// Saves from before brain crystallization have crystallized limbs and organs instead; each is turned into brain
// crystallization on load.
public class GameComponent_CrystalMigration : GameComponent
{
    public GameComponent_CrystalMigration(Game game)
    {
    }

    public override void LoadedGame()
    {
        // A copy: PawnsFinder refills one shared list on every read.
        foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_Alive.ToList())
        {
            Crystallization.MigrateLegacy(pawn);
        }
    }
}

public static class Crystallization
{
    // Parts a human could crystallize under the old per-part rule; the old percentage was out of these.
    private const float LegacyParts = 11f;

    // The brain's crystallization, or null when it has not started.
    public static Hediff? Crystal(Pawn pawn)
    {
        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            // On the brain only: a save not yet migrated may still hold crystallized limbs.
            if (hediffs[i].def == BOR_TenebralDefOf.BOR_Crystallized
                && (hediffs[i].Part?.def.tags.Contains(BodyPartTagDefOf.ConsciousnessSource) ?? false))
            {
                return hediffs[i];
            }
        }
        return null;
    }

    public static bool IsCrystallized(Pawn pawn, BodyPartRecord? part)
    {
        if (part == null)
        {
            return false;
        }
        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            if (hediffs[i].def == BOR_TenebralDefOf.BOR_Crystallized && hediffs[i].Part == part)
            {
                return true;
            }
        }
        return false;
    }

    // How far the brain has crystallized, 0 to 1.
    public static float Fraction(Pawn pawn) => Crystal(pawn)?.Severity ?? 0f;

    // Whether the pawn is crystallized enough for the transformation pod. The small tolerance absorbs float drift from
    // hundreds of tiny per-kill steps, so the kill that should reach the threshold does.
    public static bool ReadyForPod(Pawn pawn) => Fraction(pawn) >= TenebralPsychosis.BreakSettings.podThreshold - ReadyTolerance;

    private const float ReadyTolerance = 0.0001f;

    // Crystallizes the brain by `amount` more, rolling for a dark ability at every step it passes. Returns true when
    // this started the crystallization.
    public static bool Grow(Pawn pawn, float amount)
    {
        if (!pawn.RaceProps.Humanlike || pawn.Dead)
        {
            return false;
        }
        Hediff? crystal = Crystal(pawn);
        float before = crystal?.Severity ?? 0f;
        float after = Mathf.Min(1f, before + amount);
        if (crystal == null)
        {
            BodyPartRecord? brain = pawn.health.hediffSet.GetBrain();
            if (brain == null)
            {
                return false;
            }
            crystal = HediffMaker.MakeHediff(BOR_TenebralDefOf.BOR_Crystallized, pawn, brain);
            crystal.Severity = after;
            pawn.health.AddHediff(crystal, brain);
        }
        else
        {
            crystal.Severity = after;
        }
        float step = TenebralPsychosis.BreakSettings.abilityRollEvery;
        if (step > 0f && !pawn.Dead)
        {
            // Same tolerance as ReadyForPod, so the kill that readies the pawn also gets its roll.
            int rolls = Mathf.FloorToInt((after + ReadyTolerance) / step) - Mathf.FloorToInt((before + ReadyTolerance) / step);
            for (int i = 0; i < rolls; i++)
            {
                DarkAbilities.RollCrystallization(pawn);
            }
        }
        return before <= 0f;
    }

    // Replaces crystallized limbs and organs from an old save with brain crystallization worth the same share.
    public static void MigrateLegacy(Pawn pawn)
    {
        BodyPartRecord? brain = pawn.health.hediffSet.GetBrain();
        List<Hediff> legacy = pawn.health.hediffSet.hediffs
            .Where(h => h.def == BOR_TenebralDefOf.BOR_Crystallized && h.Part != brain).ToList();
        if (legacy.Count == 0 || brain == null)
        {
            return;
        }
        foreach (Hediff h in legacy)
        {
            pawn.health.RemoveHediff(h);
        }
        float severity = Mathf.Min(1f, legacy.Count / LegacyParts);
        Hediff? crystal = Crystal(pawn);
        if (crystal == null)
        {
            crystal = HediffMaker.MakeHediff(BOR_TenebralDefOf.BOR_Crystallized, pawn, brain);
            crystal.Severity = severity;
            pawn.health.AddHediff(crystal, brain);
        }
        else if (crystal.Severity < severity)
        {
            crystal.Severity = severity;
        }
    }

    public static void ClearAll(Pawn pawn)
    {
        List<Hediff> crystals = pawn.health.hediffSet.hediffs.Where(h => h.def == BOR_TenebralDefOf.BOR_Crystallized).ToList();
        foreach (Hediff h in crystals)
        {
            pawn.health.RemoveHediff(h);
        }
    }
}

// Patched via the assembly-wide PatchAll in SettingsWindowPatch.
public static class TenebralPsychosisPatches
{
    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    public static class MentalStateHandler_TryStartMentalState_Patch
    {
        // Only a berserk rage counts as a break; social fights, daze and the rest do not.
        public static void Postfix(Pawn ___pawn, bool __result, MentalStateDef stateDef, bool causedByDamage, bool causedByPsycast)
        {
            if (!__result || stateDef != MentalStateDefOf.Berserk || causedByDamage || causedByPsycast || !___pawn.RaceProps.Humanlike || !TenebralPsychosis.IsPsychotic(___pawn))
            {
                return;
            }
            TenebralPsychosis.BreakCounter(___pawn).Notify_Break();
        }
    }

    // Both work scanning and right-click orders ask HasJobOnThing before taking a job, so this covers forced work too.
    [HarmonyPatch]
    public static class WorkGiver_Scanner_HasJobOnThing_Patch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.HasJobOnThing));
            yield return AccessTools.Method(typeof(WorkGiver_Researcher), nameof(WorkGiver_Researcher.HasJobOnThing));
        }

        public static bool Prefix(WorkGiver_Scanner __instance, Pawn pawn, Thing t, ref bool __result)
        {
            if (!TenebralPsychosis.IsLimitedWork(__instance) || !TenebralPsychosis.MadeOfTenebrite(t) || !TenebralPsychosis.AtWorkLimit(pawn))
            {
                return true;
            }
            JobFailReason.Is("BOR_PsychosisLimitReached".Translate(
                TenebralPsychosis.Severity(pawn).ToStringPercent(), TenebralPsychosis.LimitComp(pawn)!.limit.ToStringPercent()));
            __result = false;
            return false;
        }
    }
}
