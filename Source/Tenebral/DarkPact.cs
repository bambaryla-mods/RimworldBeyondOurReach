using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using Verse.AI;

namespace BeyondOurReach;

// Marks a research that only a dark pact at the dark mirror can finish. Set by BOR_Research_Base_III_Pact.
public class DefModExtension_DarkPact : DefModExtension
{
}

[DefOf]
public static class BOR_DarkPactDefOf
{
    public static QuestScriptDef BOR_DarkPact = null!;
    public static JobDef BOR_DarkMirrorPact = null!;
    public static JobDef BOR_DarkMirrorUmbralPact = null!;
    public static HediffDef BOR_DarkPunishment = null!;
    public static QuestScriptDef BOR_UmbralPact = null!;
    public static ReservationLayerDef BOR_DarkMirrorCommune = null!;

    static BOR_DarkPactDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(BOR_DarkPactDefOf));
    }
}

public static class DarkPacts
{
    private static List<ResearchProjectDef>? all;

    private static readonly Dictionary<ResearchProjectDef, ThingDef?> iconDefs = new();

    public static readonly Color DarkRed = new(1f, 0.12f, 0.12f);

    // In DefDatabase order, which is stable for a given mod list; the pact job passes its research as an index into this.
    public static List<ResearchProjectDef> All => all ??= DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(r => r.HasModExtension<DefModExtension_DarkPact>()).ToList();

    // The icon a Pacts tab row shows: a building the research unlocks if it has one, else the first unlocked thing with an icon.
    // Some researches only unlock modes of other buildings; they get the mirror's icon. Altar quality pacts get the altar's.
    // The tab redraws every frame, so this is kept.
    public static ThingDef? IconDefFor(ResearchProjectDef research)
    {
        if (!iconDefs.TryGetValue(research, out ThingDef? def))
        {
            List<ThingDef> things = research.UnlockedDefs.OfType<ThingDef>().Where(d => d.uiIcon != null && d.uiIcon != BaseContent.BadTex).ToList();
            def = things.FirstOrDefault(d => d.building != null) ?? things.FirstOrDefault()
                ?? (AltarQuality.GrantedBy(research) != null ? BOR_TenebralDefOf.BOR_DarkArchotechAltar_III : null);
            iconDefs[research] = def;
        }
        return def;
    }

    // The pact under way, if any. Only one runs at a time.
    public static QuestPart_DarkPact? Active
    {
        get
        {
            List<Quest> quests = Find.QuestManager.ActiveQuestsListForReading;
            for (int i = 0; i < quests.Count; i++)
            {
                if (quests[i].State != QuestState.Ongoing)
                {
                    continue;
                }
                List<QuestPart> parts = quests[i].PartsListForReading;
                for (int j = 0; j < parts.Count; j++)
                {
                    if (parts[j] is QuestPart_DarkPact { State: QuestPartState.Enabled } pact)
                    {
                        return pact;
                    }
                }
            }
            return null;
        }
    }

    public static bool IsMirror(Thing thing) => thing.TryGetComp<CompDarkPact>() != null;

    public static IEnumerable<Thing> MirrorsOn(Map map) => map.listerBuildings.allBuildingsColonist.Where(IsMirror);

    // A broken pact's pulse: from the mirror it was made at, else any dark mirror on a home map. Returns where it came
    // from, or null when no mirror stands.
    public static Thing? Punish(Thing? mirror)
    {
        Thing? source = mirror is { Spawned: true } && mirror.Map.IsPlayerHome ? mirror
            : Find.Maps.Where(m => m.IsPlayerHome).SelectMany(MirrorsOn).FirstOrDefault();
        source?.Map.GetComponent<MapComponent_DarkPulse>()?.Emit(source.Position);
        return source;
    }

    public static bool Powered(Thing mirror) => mirror.TryGetComp<CompPowerTrader>()?.PowerOn ?? true;

    // The colony hit someone who still trusted it. Remembered for the betrayal pact, since that first hit can turn the
    // victim's faction hostile before they die.
    public static void Notify_ColonyHurt(Pawn victim)
    {
        if (victim.Dead || !victim.RaceProps.Humanlike || victim.Faction == null || victim.Faction.IsPlayer
            || victim.IsPrisoner || victim.IsSlave || victim.HostileTo(Faction.OfPlayer))
        {
            return;
        }
        Active?.Notify_Betrayal(victim);
    }
}

public class CompProperties_DarkPact : CompProperties
{
    public CompProperties_DarkPact()
    {
        compClass = typeof(CompDarkPact);
    }
}

// The dark mirror's pacts, driven from its Pacts tab (ITab_DarkPacts).
public class CompDarkPact : ThingComp
{
    private bool Communing => CommuningOtherThan(null);

    // Someone other than this pawn is communing at the mirror, for a research or an Umbral pact.
    private bool CommuningOtherThan(Pawn? self) => parent.Map.mapPawns.FreeColonistsSpawned.Any(p => p != self
        && (p.CurJobDef == BOR_DarkPactDefOf.BOR_DarkMirrorPact || p.CurJobDef == BOR_DarkPactDefOf.BOR_DarkMirrorUmbralPact));

    // Why this Umbral cannot make its pact right now, or null if it can. Its own communion does not block it.
    public string? UmbralBlockReason(Pawn umbral)
    {
        if (Umbral.GroupSteps(umbral) == 0)
        {
            return "BOR_UmbralPact_Done".Translate();
        }
        if (UmbralPacts.For(umbral) != null)
        {
            return "BOR_UmbralPact_HasPact".Translate();
        }
        if (Umbral.Transforming(umbral))
        {
            return "BOR_UmbralPact_Transforming".Translate(umbral.LabelShortCap);
        }
        if (CommuningOtherThan(umbral))
        {
            return "BOR_DarkPact_Communing".Translate();
        }
        if (!DarkPacts.Powered(parent))
        {
            return "BOR_DarkPact_NeedsPower".Translate();
        }
        if (!umbral.Spawned || umbral.Map != parent.Map)
        {
            return "BOR_UmbralPact_Away".Translate(umbral.LabelShortCap);
        }
        if (umbral.Downed)
        {
            return "BOR_DarkPact_PawnDowned".Translate().CapitalizeFirst();
        }
        if (!umbral.CanReach(parent, PathEndMode.InteractionCell, Danger.Deadly))
        {
            return "NoPath".Translate().CapitalizeFirst();
        }
        return null;
    }

    // The Umbral itself communes: no colonist menu, only the confirmation.
    public void OfferUmbral(Pawn umbral)
    {
        if (UmbralBlockReason(umbral) != null)
        {
            return;
        }
        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
            "BOR_UmbralPact_Confirm".Translate(umbral.LabelShortCap, UmbralPacts.GroupLabel(umbral), QuestNode_Root_UmbralPact.DurationLabel), () =>
            {
                if (!umbral.Dead && umbral.Spawned && UmbralBlockReason(umbral) == null)
                {
                    umbral.jobs.TryTakeOrderedJob(JobMaker.MakeJob(BOR_DarkPactDefOf.BOR_DarkMirrorUmbralPact, parent), JobTag.Misc);
                }
            }, true));
    }

    public static void ConfirmAbandonUmbral(QuestPart_UmbralPact pact) =>
        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("BOR_UmbralPact_AbandonConfirm".Translate(pact.umbral?.LabelShortCap ?? "?"),
            () => pact.Fail("BOR_DarkPact_Reason_Abandoned".Translate()), true));

    // Why a pact for this research cannot be made right now, or null if it can.
    public string? BlockReason(ResearchProjectDef r)
    {
        if (!r.PrerequisitesCompleted)
        {
            IEnumerable<ResearchProjectDef> missing = (r.prerequisites ?? new List<ResearchProjectDef>())
                .Concat(r.hiddenPrerequisites ?? new List<ResearchProjectDef>()).Where(p => !p.IsFinished);
            return "BOR_DarkPact_NeedsResearch".Translate(missing.Select(p => p.LabelCap.Resolve()).ToCommaList());
        }
        if (DarkPacts.Active is QuestPart_DarkPact active)
        {
            return "BOR_DarkPact_Active".Translate(active.research?.LabelCap ?? "");
        }
        if (Communing)
        {
            return "BOR_DarkPact_Communing".Translate();
        }
        if (!DarkPacts.Powered(parent))
        {
            return "BOR_DarkPact_NeedsPower".Translate();
        }
        if (r.requiresMechanitor && !MechanitorUtility.AnyMechanitorInPlayerFaction())
        {
            return "BOR_DarkPact_NeedsMechanitor".Translate();
        }
        return null;
    }

    public void Offer(ResearchProjectDef research)
    {
        if (BlockReason(research) == null)
        {
            ChoosePawn(research);
        }
    }

    public static void ConfirmAbandon() =>
        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("BOR_DarkPact_AbandonConfirm".Translate(),
            () => DarkPacts.Active?.Fail("BOR_DarkPact_Reason_Abandoned".Translate()), true));

    private void ChoosePawn(ResearchProjectDef research)
    {
        List<FloatMenuOption> options = new();
        foreach (Pawn pawn in parent.Map.mapPawns.FreeColonistsSpawned)
        {
            Pawn p = pawn;
            if (p.Downed)
            {
                options.Add(new FloatMenuOption(p.LabelShortCap + " (" + "BOR_DarkPact_PawnDowned".Translate() + ")", null));
            }
            else if (!p.CanReach(parent, PathEndMode.InteractionCell, Danger.Deadly))
            {
                options.Add(new FloatMenuOption(p.LabelShortCap + " (" + "NoPath".Translate() + ")", null));
            }
            else
            {
                options.Add(new FloatMenuOption(p.LabelShortCap, () => Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "BOR_DarkPact_Confirm".Translate(p.LabelShortCap, research.LabelCap, QuestNode_Root_DarkPact.DurationLabel), () => Order(p, research), true))));
            }
        }
        if (options.Count == 0)
        {
            options.Add(new FloatMenuOption("BOR_DarkPact_NoColonists".Translate(), null));
        }
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private void Order(Pawn pawn, ResearchProjectDef research)
    {
        if (pawn.Dead || !pawn.Spawned || DarkPacts.Active != null)
        {
            return;
        }
        Job job = JobMaker.MakeJob(BOR_DarkPactDefOf.BOR_DarkMirrorPact, parent);
        job.count = DarkPacts.All.IndexOf(research);
        pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
    }

    // The communion is over: the Dark Archotechs name their price.
    public void StartPact(Pawn pawn, ResearchProjectDef research)
    {
        if (DarkPacts.Active != null || research.IsFinished || !research.PrerequisitesCompleted)
        {
            return;
        }
        Slate slate = new();
        slate.Set("research", research);
        slate.Set("pawn", pawn);
        slate.Set("mirror", (Thing)parent);
        Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(BOR_DarkPactDefOf.BOR_DarkPact, slate);
        if (quest == null)
        {
            return;
        }
        QuestUtility.SendLetterQuestAvailable(quest);
        DarkPulse.Flash(parent.Map, parent.TrueCenter(), 4f);
    }

    public override string CompInspectStringExtra()
    {
        QuestPart_DarkPact? active = DarkPacts.Active;
        if (active?.research == null)
        {
            return null!;
        }
        return "BOR_DarkPact_Inspect".Translate(active.research.LabelCap, active.StatusLine, active.TicksLeft.ToStringTicksToPeriod());
    }
}

public class JobDriver_DarkMirrorPact : JobDriver
{
    private const TargetIndex MirrorInd = TargetIndex.A;
    private const int WorkTicks = 600;
    private const int FlashInterval = 60;

    private int ticksDone;

    protected Thing Mirror => job.GetTarget(MirrorInd).Thing;

    private ResearchProjectDef? Research => job.count >= 0 && job.count < DarkPacts.All.Count ? DarkPacts.All[job.count] : null;

    // On its own layer: the mirror is also the tenebral pod, and communing must not block pawns entering it.
    public override bool TryMakePreToilReservations(bool errorOnFailed) =>
        pawn.Reserve(Mirror, job, 1, -1, BOR_DarkPactDefOf.BOR_DarkMirrorCommune, errorOnFailed);

    // The communion is called off when this turns true.
    protected virtual bool Invalid => !DarkPacts.Powered(Mirror) || DarkPacts.Active != null || Research == null || Research.IsFinished;

    // What the finished communion starts.
    protected virtual void Conclude()
    {
        if (Research is ResearchProjectDef research)
        {
            Mirror.TryGetComp<CompDarkPact>()?.StartPact(pawn, research);
        }
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(MirrorInd);
        this.FailOn(() => Invalid);

        yield return Toils_Goto.GotoThing(MirrorInd, PathEndMode.InteractionCell);

        Toil commune = ToilMaker.MakeToil("DarkMirrorPact");
        commune.initAction = () => ticksDone = 0;
        commune.tickAction = () =>
        {
            ticksDone++;
            pawn.rotationTracker.FaceTarget(Mirror);
            if (ticksDone % FlashInterval == 0)
            {
                DarkPulse.Flash(Mirror.Map, Mirror.TrueCenter(), 1.5f + 2f * ticksDone / WorkTicks);
            }
            if (ticksDone >= WorkTicks)
            {
                ReadyForNextToil();
            }
        };
        commune.defaultCompleteMode = ToilCompleteMode.Never;
        commune.WithProgressBar(MirrorInd, () => (float)ticksDone / WorkTicks);
        yield return commune;

        yield return Toils_General.Do(Conclude);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref ticksDone, "ticksDone");
    }
}

// An Umbral communing at the mirror for its own next transformation group.
public class JobDriver_DarkMirrorUmbralPact : JobDriver_DarkMirrorPact
{
    private const int CheckInterval = 30;

    // The block reasons scan quests and pawns and path to the mirror, so they are checked twice a second, not every tick.
    protected override bool Invalid =>
        pawn.IsHashIntervalTick(CheckInterval) && Mirror.TryGetComp<CompDarkPact>()?.UmbralBlockReason(pawn) != null;

    protected override void Conclude() => UmbralPacts.Start(pawn, Mirror);
}
