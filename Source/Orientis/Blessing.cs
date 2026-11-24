using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace BeyondOurReach;

public class CompProperties_BlessingBench : CompProperties
{
    public List<BodyPartDef> blessableParts = new();

    // Meditation time for each part chosen.
    public int ticksPerPart = 1250;

    public CompProperties_BlessingBench()
    {
        compClass = typeof(CompBlessingBench);
    }
}

// Holds the parts chosen for the pawn on their way here. One pawn at a time, as the job reserves the bench.
public class CompBlessingBench : ThingComp, IGlowSource
{
    private Pawn? pawn;
    private List<int> partIndices = new();

    public CompProperties_BlessingBench Props => (CompProperties_BlessingBench)props;

    public int PartCount => partIndices.Count;

    private int lastBlessTick = -999;

    // Lit while a pawn is meditating here.
    public bool GlowActive => Find.TickManager.TicksGame - lastBlessTick <= 2;

    public void NotifyBlessing() => lastBlessTick = Find.TickManager.TicksGame;

    public IEnumerable<BodyPartRecord> BlessableParts(Pawn target) => Blessings.Blessable(target, Props.blessableParts);

    public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
    {
        if (!Orientis.IsOrientis(selPawn))
        {
            yield return new FloatMenuOption("BOR_BlessingOrientisOnly".Translate(), null);
            yield break;
        }
        if (!selPawn.CanReserveAndReach(parent, PathEndMode.InteractionCell, Danger.Deadly))
        {
            yield return new FloatMenuOption("CannotUseNoPath".Translate(), null);
            yield break;
        }
        yield return new FloatMenuOption("BOR_BlessingOption".Translate(), () => Find.WindowStack.Add(new Window_Blessing(this, selPawn)), BorIcons.OrientisBlessing, Color.white);
    }

    public void Begin(Pawn target, List<BodyPartRecord> parts)
    {
        pawn = target;
        partIndices = parts.Select(p => target.RaceProps.body.GetIndexOfPart(p)).ToList();
        target.jobs.TryTakeOrderedJob(JobMaker.MakeJob(BOR_OrientisDefOf.BOR_OrientisBless, parent), JobTag.Misc);
    }

    // A part that is already blessed starts its quadrum over; blessings do not stack.
    public void Complete(Pawn target)
    {
        if (target != pawn)
        {
            return;
        }
        List<BodyPartRecord> all = target.RaceProps.body.AllParts;
        foreach (int index in partIndices)
        {
            BodyPartRecord part = all[index];
            if (target.health.hediffSet.PartIsMissing(part))
            {
                continue;
            }
            Hediff? old = target.health.hediffSet.hediffs.FirstOrDefault(h => h.def == BOR_OrientisDefOf.BOR_Blessing && h.Part == part);
            if (old != null)
            {
                target.health.RemoveHediff(old);
            }
            target.health.AddHediff(BOR_OrientisDefOf.BOR_Blessing, part);
        }
        Messages.Message("BOR_BlessingDone".Translate(target.LabelShortCap, partIndices.Count), target, MessageTypeDefOf.PositiveEvent);
        pawn = null;
        partIndices.Clear();
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_References.Look(ref pawn, "blessingPawn");
        Scribe_Collections.Look(ref partIndices, "blessingParts", LookMode.Value);
        partIndices ??= new List<int>();
    }
}

public class JobDriver_OrientisBless : JobDriver
{
    private int ticksDone;

    private Thing Bench => job.GetTarget(TargetIndex.A).Thing;

    private CompBlessingBench Comp => Bench.TryGetComp<CompBlessingBench>();

    public override bool TryMakePreToilReservations(bool errorOnFailed) =>
        pawn.Reserve(Bench, job, 1, -1, null, errorOnFailed);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

        Toil meditate = ToilMaker.MakeToil("OrientisBless");
        meditate.tickAction = () =>
        {
            pawn.rotationTracker.FaceTarget(Bench);
            ticksDone++;
            Comp.NotifyBlessing();
            if (ticksDone >= Total)
            {
                ReadyForNextToil();
            }
        };
        meditate.defaultCompleteMode = ToilCompleteMode.Never;
        meditate.WithProgressBar(TargetIndex.A, () => (float)ticksDone / Total);
        meditate.handlingFacing = true;
        yield return meditate;

        Toil finish = ToilMaker.MakeToil("OrientisBlessFinish");
        finish.initAction = () => Comp.Complete(pawn);
        finish.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return finish;
    }

    private int Total => Mathf.Max(1, Comp.PartCount * Comp.Props.ticksPerPart);

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref ticksDone, "ticksDone");
    }
}

// Blessings outside the bench: which parts can take one, and the permanent touch of the holy an expedition can give.
// The touch is read by def name so this works whether or not the mod that grants it is loaded.
public static class Blessings
{
    private static HediffDef? touch;
    private static List<BodyPartDef>? benchParts;

    public static HediffDef? Touch => touch ??= DefDatabase<HediffDef>.GetNamedSilentFail("BOR_TouchOfTheHoly");

    // The parts any blessing bench can bless, read off the bench defs.
    public static List<BodyPartDef> BenchParts => benchParts ??= DefDatabase<ThingDef>.AllDefsListForReading
        .Select(d => d.GetCompProperties<CompProperties_BlessingBench>())
        .Where(p => p != null).SelectMany(p => p!.blessableParts).Distinct().ToList();

    public static bool Touched(Pawn pawn, BodyPartRecord part) =>
        Touch != null && pawn.health.hediffSet.hediffs.Any(h => h.def == Touch && h.Part == part);

    public static bool HasTouch(Pawn pawn) => Touch != null && pawn.health.hediffSet.HasHediff(Touch);

    // Natural, present parts of these kinds that no touch of the holy already holds: a touched part is blessed for good.
    public static IEnumerable<BodyPartRecord> Blessable(Pawn target, List<BodyPartDef> defs)
    {
        HediffSet set = target.health.hediffSet;
        return target.RaceProps.body.AllParts.Where(p => defs.Contains(p.def)
            && !set.PartIsMissing(p) && !set.PartOrAnyAncestorHasDirectlyAddedParts(p) && !Touched(target, p));
    }
}

public class Window_Blessing : BorWindow
{
    private const float RowHeight = 28f;

    private readonly CompBlessingBench bench;
    private readonly Pawn pawn;
    private readonly List<BodyPartRecord> parts;
    private readonly HashSet<BodyPartRecord> chosen = new();
    private Vector2 scroll;

    public override Vector2 InitialSize => new(460f, 600f);

    public Window_Blessing(CompBlessingBench bench, Pawn pawn)
    {
        this.bench = bench;
        this.pawn = pawn;
        parts = bench.BlessableParts(pawn).ToList();
        forcePause = true;
        doCloseX = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = false;
    }

    protected override string Title => "BOR_BlessingTitle".Translate(pawn.LabelShortCap);

    protected override BorUISchemeDef Scheme => BOR_UISchemeDefOf.BOR_UIScheme_Orientis;

    protected override void Contents(Rect inRect)
    {
        Rect outRect = new(inRect.x, inRect.y, inRect.width, inRect.height - 50f);
        Rect viewRect = new(0f, 0f, outRect.width - 16f, parts.Count * RowHeight);
        Widgets.BeginScrollView(outRect, ref scroll, viewRect);
        float y = 0f;
        foreach (BodyPartRecord part in parts)
        {
            bool blessed = pawn.health.hediffSet.hediffs.Any(h => h.def == BOR_OrientisDefOf.BOR_Blessing && h.Part == part);
            string label = part.LabelCap + (blessed ? " (" + "BOR_BlessingAlready".Translate() + ")" : "");
            bool on = chosen.Contains(part);
            BorSkin.Current.Checkbox(new Rect(0f, y, viewRect.width, RowHeight), label, ref on);
            if (on)
            {
                chosen.Add(part);
            }
            else
            {
                chosen.Remove(part);
            }
            y += RowHeight;
        }
        Widgets.EndScrollView();
        Rect button = new(inRect.xMax - 160f, inRect.yMax - 40f, 160f, 40f);
        if (BorSkin.Current.Button(button, "BOR_BlessingBegin".Translate(), active: chosen.Count > 0, tone: BorTone.Good))
        {
            Close();
            bench.Begin(pawn, chosen.ToList());
        }
    }
}
