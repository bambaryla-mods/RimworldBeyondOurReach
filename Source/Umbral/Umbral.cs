using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using LudeonTK;
using RimWorld;
using Verse;

namespace BeyondOurReach;

public static class Umbral
{
    public const float FixedMood = 0.5f;

    // Cached per pawn instance because the forbid check asks every tick; cleared when the xenotype or the
    // BOR_Umbral hediff changes. Pawns loaded from a save are new instances, so nothing stale survives a load.
    private static readonly ConditionalWeakTable<Pawn, StrongBox<bool>> umbralCache = new();

    // The BOR_Umbral hediff is the fallback when Biotech (and so the xenotype) is not active.
    public static bool IsUmbral(Pawn pawn)
    {
        if (!umbralCache.TryGetValue(pawn, out StrongBox<bool>? cached))
        {
            cached = new StrongBox<bool>(ComputeIsUmbral(pawn));
            umbralCache.Add(pawn, cached);
        }
        return cached.Value;
    }

    private static bool ComputeIsUmbral(Pawn pawn) =>
        (pawn.genes != null && BOR_UmbralDefOf.BOR_UmbralXenotype != null && pawn.genes.Xenotype == BOR_UmbralDefOf.BOR_UmbralXenotype)
        || HasMarker(pawn);

    public static void InvalidateUmbral(Pawn pawn) => umbralCache.Remove(pawn);

    private static bool HasMarker(Pawn pawn) => pawn.health?.hediffSet?.HasHediff(BOR_UmbralDefOf.BOR_Umbral) ?? false;

    private static bool HasGeneOrMarker(Pawn pawn, GeneDef? gene) =>
        (gene != null && pawn.genes != null && pawn.genes.HasActiveGene(gene)) || HasMarker(pawn);

    public static bool HasPsychosisResistance(Pawn pawn) => HasGeneOrMarker(pawn, BOR_UmbralDefOf.BOR_UmbralPsychosisResistance);

    public static bool BlocksBodyParts(Pawn pawn) => HasGeneOrMarker(pawn, BOR_UmbralDefOf.BOR_UmbralNoBodyParts);

    public static bool Transforms(Pawn pawn) => HasGeneOrMarker(pawn, BOR_UmbralDefOf.BOR_UmbralTransformationGene);

    public static DefModExtension_UmbralTransformation Settings =>
        BOR_UmbralDefOf.BOR_UmbralTransformation.GetModExtension<DefModExtension_UmbralTransformation>() ?? new DefModExtension_UmbralTransformation();

    // Parts the pawn still has to have replaced in phase A.
    public static IEnumerable<(BodyPartRecord part, HediffDef hediff)> PendingReplacements(Pawn pawn)
    {
        HediffSet set = pawn.health.hediffSet;
        foreach (UmbralPartReplacement r in Settings.replacements)
        {
            foreach (BodyPartRecord part in pawn.RaceProps.body.GetPartsWithDef(r.bodyPart))
            {
                if (set.PartIsMissing(part) && part.parent != null && set.PartIsMissing(part.parent))
                {
                    continue;
                }
                if (!set.hediffs.Any(h => h.def == r.hediff && h.Part == part))
                {
                    yield return (part, r.hediff);
                }
            }
        }
    }

    public enum Phase
    {
        A,
        B,
        C,
        Done
    }

    public static Phase CurrentPhase(Pawn pawn)
    {
        if (PendingReplacements(pawn).Any())
        {
            return Phase.A;
        }
        HediffSet set = pawn.health.hediffSet;
        if (!set.HasHediff(BOR_UmbralDefOf.BOR_UmbralExoskeleton) || !set.HasHediff(BOR_UmbralDefOf.BOR_UmbralExoskull))
        {
            return Phase.B;
        }
        return set.HasHediff(BOR_UmbralDefOf.BOR_UmbralUtilityPod) ? Phase.Done : Phase.C;
    }

    public static bool Transforming(Pawn pawn) => pawn.health?.hediffSet?.HasHediff(BOR_UmbralDefOf.BOR_UmbralTransformation) ?? false;

    // How many steps the pawn's next pact grants. A group never crosses into the next phase; 0 once fully transformed.
    public static int GroupSteps(Pawn pawn)
    {
        switch (CurrentPhase(pawn))
        {
            case Phase.A:
                return System.Math.Min(Settings.partsPerPact, PendingReplacements(pawn).Count());
            case Phase.B:
                HediffSet set = pawn.health.hediffSet;
                return (set.HasHediff(BOR_UmbralDefOf.BOR_UmbralExoskeleton) ? 0 : 1) + (set.HasHediff(BOR_UmbralDefOf.BOR_UmbralExoskull) ? 0 : 1);
            case Phase.C:
                return 1;
            default:
                return 0;
        }
    }

    // Starts the transformation illness for a whole group: each step that completes starts the next until the group is granted.
    // A pawn already transforming (an illness from an older save) carries the group on after its current step.
    public static void StartGroup(Pawn pawn)
    {
        int steps = GroupSteps(pawn);
        if (steps <= 0)
        {
            return;
        }
        HediffComp_UmbralTransformation? running = pawn.health.hediffSet.GetFirstHediffOfDef(BOR_UmbralDefOf.BOR_UmbralTransformation)
            ?.TryGetComp<HediffComp_UmbralTransformation>();
        if (running != null)
        {
            running.stepsLeft += steps;
            return;
        }
        StartStep(pawn, steps - 1);
    }

    private static void StartStep(Pawn pawn, int stepsLeft)
    {
        Hediff hediff = pawn.health.AddHediff(BOR_UmbralDefOf.BOR_UmbralTransformation);
        HediffComp_UmbralTransformation? comp = hediff.TryGetComp<HediffComp_UmbralTransformation>();
        if (comp != null)
        {
            comp.stepsLeft = stepsLeft;
        }
    }

    // The illness finished a step; the next one of the group, if any, starts straight away. A group ends with its phase.
    public static void Notify_StepDone(Pawn pawn, int stepsLeft)
    {
        Phase phase = CurrentPhase(pawn);
        CompleteTransformation(pawn);
        if (stepsLeft > 0 && !pawn.Dead && CurrentPhase(pawn) == phase && phase != Phase.Done && !Transforming(pawn))
        {
            StartStep(pawn, stepsLeft - 1);
        }
    }

    // Grants the next transformation step: an Umbral part, then exoskeleton, exoskull and finally the utility pod.
    public static void CompleteTransformation(Pawn pawn)
    {
        string? granted = null;
        switch (CurrentPhase(pawn))
        {
            case Phase.A:
                if (PendingReplacements(pawn).TryRandomElement(out var pick))
                {
                    if (pick.hediff.hediffClass != null && typeof(Hediff_AddedPart).IsAssignableFrom(pick.hediff.hediffClass))
                    {
                        pawn.health.RestorePart(pick.part);
                    }
                    pawn.health.AddHediff(pick.hediff, pick.part);
                    granted = pick.hediff.label;
                }
                break;
            case Phase.B:
                HediffDef next = pawn.health.hediffSet.HasHediff(BOR_UmbralDefOf.BOR_UmbralExoskeleton)
                    ? BOR_UmbralDefOf.BOR_UmbralExoskull
                    : BOR_UmbralDefOf.BOR_UmbralExoskeleton;
                pawn.health.AddHediff(next);
                granted = next.label;
                break;
            case Phase.C:
                pawn.health.AddHediff(BOR_UmbralDefOf.BOR_UmbralUtilityPod);
                granted = BOR_UmbralDefOf.BOR_UmbralUtilityPod.label;
                break;
        }
        if (granted == null)
        {
            return;
        }
        if (pawn.Faction == Faction.OfPlayer)
        {
            Messages.Message("BOR_UmbralTransformed".Translate(pawn.LabelShortCap, granted), pawn, MessageTypeDefOf.PositiveEvent);
        }
        DarkAbilities.RollUmbralTransformation(pawn);
    }

    // Turns a crystallized tenebral pawn into an Umbral, as it leaves a Tenebral pod in transformation mode.
    public static void MakeUmbral(Pawn pawn)
    {
        Crystallization.ClearAll(pawn);
        List<Hediff> tenebral = pawn.health.hediffSet.hediffs
            .Where(h => h.def.HasModExtension<DefModExtension_TenebralImplant>()
                || h.def == BOR_TenebralDefOf.BOR_TenebralPsychosis
                || h.def == BOR_TenebralDefOf.BOR_TenebralBreakCounter)
            .ToList();
        foreach (Hediff h in tenebral)
        {
            if (h is Hediff_AddedPart && h.Part != null)
            {
                pawn.health.RestorePart(h.Part);
            }
            else if (pawn.health.hediffSet.hediffs.Contains(h))
            {
                pawn.health.RemoveHediff(h);
            }
        }
        if (ModsConfig.BiotechActive && pawn.genes != null && BOR_UmbralDefOf.BOR_UmbralXenotype != null)
        {
            pawn.genes.SetXenotype(BOR_UmbralDefOf.BOR_UmbralXenotype);
        }
        if (!HasMarker(pawn))
        {
            pawn.health.AddHediff(BOR_UmbralDefOf.BOR_Umbral);
        }
        pawn.needs?.AddOrRemoveNeedsAsAppropriate();
        Hollow(pawn);
        AdoptUmbralIdeo(pawn);
    }

    // Umbrals feel nothing: traits and memories go here, and UmbralPatches blocks every new thought and fixes the mood.
    private static void Hollow(Pawn pawn)
    {
        if (pawn.story?.traits != null)
        {
            foreach (Trait trait in pawn.story.traits.allTraits.ToList())
            {
                pawn.story.traits.RemoveTrait(trait);
            }
        }
        Need_Mood? mood = pawn.needs?.mood;
        if (mood != null)
        {
            mood.thoughts.memories.Memories.Clear();
            mood.thoughts.situational.Notify_SituationalThoughtsDirty();
            mood.CurLevel = FixedMood;
        }
    }

    // A colonist who turns Umbral takes up the Umbral ideoligion; the rest of the colony keeps theirs.
    private static void AdoptUmbralIdeo(Pawn pawn)
    {
        if (!ModsConfig.IdeologyActive || pawn.ideo == null || pawn.Faction != Faction.OfPlayer)
        {
            return;
        }
        Ideo? ideo = Find.FactionManager.FirstFactionOfDef(BOR_UmbralDefOf.BOR_Umbrals)?.ideos?.PrimaryIdeo;
        if (ideo == null || pawn.Ideo == ideo)
        {
            return;
        }
        // Spawning reveals a hidden ideoligion, but this pawn may already be spawned.
        ideo.hidden = false;
        pawn.ideo.SetIdeo(ideo);
    }

    public static void RemoveHediffs(Pawn pawn, HediffDef def)
    {
        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int i = hediffs.Count - 1; i >= 0; i--)
        {
            if (hediffs[i].def == def)
            {
                pawn.health.RemoveHediff(hediffs[i]);
            }
        }
    }

    public static bool WearsExoskeleton(Pawn pawn) => pawn.health?.hediffSet?.HasHediff(BOR_UmbralDefOf.BOR_UmbralExoskeleton) ?? false;

    public static bool HasUtilityPod(Pawn pawn) => pawn.health?.hediffSet?.HasHediff(BOR_UmbralDefOf.BOR_UmbralUtilityPod) ?? false;

    public static bool IsExoApparel(ThingDef def) =>
        ExoApparel(BOR_UmbralDefOf.BOR_UmbralExoskeleton).Contains(def) || ExoApparel(BOR_UmbralDefOf.BOR_UmbralExoskull).Contains(def);

    private static List<ThingDef> ExoApparel(HediffDef hediff) =>
        hediff.CompProps<HediffCompProperties_UmbralModeApparel>()?.apparelByMode ?? new List<ThingDef>();

    public static bool IsUtilityWeapon(ThingDef def) =>
        BOR_UmbralDefOf.BOR_UmbralUtilityPod.CompProps<HediffCompProperties_UmbralUtilityPod>()?.weapons.Contains(def) ?? false;
}

// The one thought an Umbral has. It changes nothing; the mood is fixed elsewhere.
public class ThoughtWorker_UmbralEmpty : ThoughtWorker
{
    protected override ThoughtState CurrentStateInternal(Pawn p) =>
        Umbral.IsUmbral(p) ? ThoughtState.ActiveDefault : ThoughtState.Inactive;
}

// Flu-style progression; at full severity the pawn does not die but gains its next Umbral step. stepsLeft counts the steps
// of the pact's group still to come after this one; illnesses saved before pacts load with 0 and grant one step.
public class HediffComp_UmbralTransformation : HediffComp
{
    public int stepsLeft;

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref stepsLeft, "stepsLeft");
    }

    public override void CompPostTickInterval(ref float severityAdjustment, int delta)
    {
        base.CompPostTickInterval(ref severityAdjustment, delta);
        // The severity comp runs first and only adds progress on some ticks.
        if (severityAdjustment > 0f && RighteousSlaughter.IsBlessed(Pawn))
        {
            severityAdjustment *= RighteousSlaughter.Settings.transformationSpeed;
        }
        if (parent.Severity + severityAdjustment < parent.def.maxSeverity)
        {
            return;
        }
        severityAdjustment = 0f;
        Pawn pawn = Pawn;
        int left = stepsLeft;
        pawn.health.RemoveHediff(parent);
        Umbral.Notify_StepDone(pawn, left);
    }
}

public static class UmbralDebug
{
    [DebugAction("Pawns", "Start Umbral pact", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void StartPact(Pawn pawn)
    {
        Thing? mirror = DarkPacts.MirrorsOn(pawn.Map).FirstOrDefault();
        if (mirror != null)
        {
            UmbralPacts.Start(pawn, mirror);
        }
    }

    [DebugAction("Pawns", "Start Umbral pact group", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void StartPactGroup(Pawn pawn)
    {
        if (Umbral.IsUmbral(pawn))
        {
            Umbral.StartGroup(pawn);
        }
    }
}
