using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace BeyondOurReach;

public enum UmbralPactDemand
{
    Slaughter,
    IntoTheDark,
    Offering,
    SeverTie,
    Vigil
}

// Umbral pacts: each Umbral bargains at the dark mirror for its next transformation group. Unlike the research pacts,
// every Umbral may have one under way at once. See openspec/specs/dark-pacts.
public static class UmbralPacts
{
    public static IEnumerable<QuestPart_UmbralPact> Active
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
                    if (parts[j] is QuestPart_UmbralPact { State: QuestPartState.Enabled } pact)
                    {
                        yield return pact;
                    }
                }
            }
        }
    }

    public static QuestPart_UmbralPact? For(Pawn umbral) => Active.FirstOrDefault(p => p.umbral == umbral);

    // A colony Umbral that still has transformation steps to bargain for.
    public static bool Eligible(Pawn pawn) =>
        pawn.RaceProps.Humanlike && pawn.Faction == Faction.OfPlayer && Umbral.IsUmbral(pawn) && Umbral.Transforms(pawn) && Umbral.GroupSteps(pawn) > 0;

    // What the pawn's next pact grants.
    public static string GroupLabel(Pawn pawn)
    {
        int steps = Umbral.GroupSteps(pawn);
        return Umbral.CurrentPhase(pawn) switch
        {
            Umbral.Phase.A => PartsLabel(pawn, steps),
            Umbral.Phase.B => "BOR_UmbralPact_GroupExo".Translate(),
            Umbral.Phase.C => "BOR_UmbralPact_GroupPod".Translate(),
            _ => "BOR_UmbralPact_GroupDone".Translate()
        };
    }

    // "Umbral parts 5-8 of 16": the parts already grown count toward the total.
    private static string PartsLabel(Pawn pawn, int steps)
    {
        int pending = Umbral.PendingReplacements(pawn).Count();
        HashSet<HediffDef> partDefs = new(Umbral.Settings.replacements.Select(r => r.hediff));
        int grown = pawn.health.hediffSet.hediffs.Count(h => partDefs.Contains(h.def));
        int total = grown + pending;
        string range = steps > 1 ? (grown + 1) + "-" + (grown + steps) : (grown + 1).ToString();
        return "BOR_UmbralPact_GroupParts".Translate(range, total);
    }

    // The communion is over: the Dark Archotechs name the Umbral's price.
    public static void Start(Pawn umbral, Thing mirror)
    {
        if (For(umbral) != null || !Eligible(umbral) || Umbral.Transforming(umbral) || !mirror.Spawned)
        {
            return;
        }
        Slate slate = new();
        slate.Set("umbral", umbral);
        slate.Set("mirror", mirror);
        Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(BOR_DarkPactDefOf.BOR_UmbralPact, slate);
        if (quest == null)
        {
            return;
        }
        QuestUtility.SendLetterQuestAvailable(quest);
        DarkPulse.Flash(mirror.Map, mirror.TrueCenter(), 4f);
    }

    // One slaughter, by any Umbral, counts for every Umbral pact asking for one.
    public static void Notify_Slaughter()
    {
        foreach (QuestPart_UmbralPact pact in Active.ToList())
        {
            if (pact.demand == UmbralPactDemand.Slaughter)
            {
                pact.Count();
            }
        }
    }

    // Every pact naming this pawn is paid by the one pod entry.
    public static void Notify_EnteredPod(Pawn pawn)
    {
        foreach (QuestPart_UmbralPact pact in Active.ToList())
        {
            if (pact.demand == UmbralPactDemand.IntoTheDark && pact.target == pawn)
            {
                pact.Succeed();
            }
        }
    }

    private static int offeringsCheckedTick = -1;

    // One offering pile pays every Umbral pact asking for an offering, and is taken once. Every Offering pact calls this on
    // the same long tick; only the first call scans.
    public static void TryPayOfferings()
    {
        int now = Find.TickManager.TicksGame;
        if (offeringsCheckedTick == now)
        {
            return;
        }
        offeringsCheckedTick = now;
        List<QuestPart_UmbralPact> due = Active.Where(p => p.demand == UmbralPactDemand.Offering).ToList();
        if (due.Count == 0)
        {
            return;
        }
        QuestNode_Root_UmbralPact s = QuestNode_Root_UmbralPact.Settings;
        foreach (Map map in Find.Maps)
        {
            foreach (Thing mirror in DarkPacts.MirrorsOn(map))
            {
                List<Thing> things = GenRadial.RadialDistinctThingsAround(mirror.Position, map, s.offeringRadius, true)
                    .Where(t => t.def == BOR_TenebralDefOf.BOR_Tenebrite || t.def == BOR_TenebralDefOf.BOR_ComponentIII).ToList();
                if (Total(things, BOR_TenebralDefOf.BOR_Tenebrite) < s.tenebrite || Total(things, BOR_TenebralDefOf.BOR_ComponentIII) < s.joints)
                {
                    continue;
                }
                foreach (QuestPart_UmbralPact pact in due)
                {
                    pact.Succeed();
                }
                Consume(things, BOR_TenebralDefOf.BOR_Tenebrite, s.tenebrite);
                Consume(things, BOR_TenebralDefOf.BOR_ComponentIII, s.joints);
                return;
            }
        }
    }

    private static int Total(List<Thing> things, ThingDef def) => things.Where(t => t.def == def).Sum(t => t.stackCount);

    private static void Consume(List<Thing> things, ThingDef def, int count)
    {
        foreach (Thing thing in things.Where(t => t.def == def))
        {
            if (count <= 0)
            {
                return;
            }
            int take = System.Math.Min(count, thing.stackCount);
            count -= take;
            thing.SplitOff(take).Destroy();
        }
    }
}

// Rolls an Umbral pact's price. The counts are tuned in the BOR_UmbralPact quest script.
public class QuestNode_Root_UmbralPact : QuestNode
{
    public float durationDays = 10f;
    public int slaughters = 1;
    public int tenebrite = 150;
    public int joints = 10;
    public float offeringRadius = 10f;
    // The vigil costs little more than the Umbral's absence from fights, so it comes up less often.
    public float vigilWeight = 0.5f;

    public static QuestNode_Root_UmbralPact Settings => (QuestNode_Root_UmbralPact)BOR_DarkPactDefOf.BOR_UmbralPact.root;

    public static int DurationTicks => (int)(Settings.durationDays * GenDate.TicksPerDay);

    public static string DurationLabel => DurationTicks.ToStringTicksToPeriod();

    protected override bool TestRunInt(Slate slate) => true;

    protected override void RunInt()
    {
        Slate slate = QuestGen.slate;
        Quest quest = QuestGen.quest;
        Pawn umbral = slate.Get<Pawn>("umbral");
        QuestPart_UmbralPact part = new()
        {
            umbral = umbral,
            mirror = slate.Get<Thing>("mirror"),
            durationTicks = DurationTicks,
            inSignalEnable = slate.Get<string>("inSignal")
        };

        List<(UmbralPactDemand demand, float weight)> pool = new()
        {
            (UmbralPactDemand.Slaughter, 1f), (UmbralPactDemand.Offering, 1f), (UmbralPactDemand.Vigil, vigilWeight)
        };
        Pawn? convert = PickConvert(umbral);
        if (convert != null)
        {
            pool.Add((UmbralPactDemand.IntoTheDark, 1f));
        }
        Pawn? tie = PickTie(umbral);
        if (tie != null)
        {
            pool.Add((UmbralPactDemand.SeverTie, 1f));
        }

        part.demand = pool.RandomElementByWeight(e => e.weight).demand;
        switch (part.demand)
        {
            case UmbralPactDemand.Slaughter:
                part.goal = slaughters;
                part.objectiveText = "BOR_UmbralPact_Obj_Slaughter".Translate(slaughters);
                break;
            case UmbralPactDemand.IntoTheDark:
                part.target = convert;
                part.objectiveText = "BOR_UmbralPact_Obj_IntoTheDark".Translate(convert!.LabelShortCap);
                break;
            case UmbralPactDemand.Offering:
                part.objectiveText = "BOR_UmbralPact_Obj_Offering".Translate(tenebrite, joints, offeringRadius.ToString("0"));
                break;
            case UmbralPactDemand.SeverTie:
                part.target = tie;
                string relation = umbral.GetMostImportantRelation(tie)?.GetGenderSpecificLabel(tie) ?? "";
                part.objectiveText = "BOR_UmbralPact_Obj_SeverTie".Translate(umbral.LabelShortCap, tie!.LabelShortCap, relation);
                break;
            case UmbralPactDemand.Vigil:
                part.objectiveText = "BOR_UmbralPact_Obj_Vigil".Translate(umbral.LabelShortCap);
                break;
        }
        quest.AddPart(part);
        slate.Set("resolvedQuestName", "BOR_UmbralPact_QuestName".Translate(umbral.LabelShortCap).Resolve());
        slate.Set("resolvedQuestDescription",
            "BOR_UmbralPact_QuestDescription".Translate(umbral.LabelShortCap, UmbralPacts.GroupLabel(umbral), part.objectiveText, DurationLabel).Resolve());
    }

    // A colonist crystallized enough for the transformation pod and not in one yet. Another Umbral's pact may name them too.
    private static Pawn? PickConvert(Pawn umbral)
    {
        float needed = TenebralPsychosis.BreakSettings.podThreshold;
        return PawnsFinder.AllMaps_FreeColonistsSpawned
            .Where(p => p != umbral && p.RaceProps.Humanlike && !Umbral.IsUmbral(p) && p.Map.IsPlayerHome && Crystallization.Fraction(p) >= needed)
            .RandomElementWithFallback();
    }

    // The Umbral's own adult blood relative or partner, on a home map and not an enemy: a colonist, prisoner, slave or guest.
    private static Pawn? PickTie(Pawn umbral)
    {
        if (umbral.relations == null)
        {
            return null;
        }
        return umbral.relations.FamilyByBlood
            .Concat(LovePartnerRelationUtility.ExistingLovePartners(umbral, false).Select(r => r.otherPawn))
            .Distinct()
            .Where(r => r != umbral && !r.Dead && r.Spawned && r.Map.IsPlayerHome && r.RaceProps.Humanlike && r.DevelopmentalStage.Adult()
                && (r.IsPrisonerOfColony || !r.HostileTo(Faction.OfPlayer)))
            .RandomElementWithFallback();
    }
}

// Tracks one Umbral's pact: counts the deed, watches the Umbral, its target and the clock, and settles the bargain.
public class QuestPart_UmbralPact : QuestPartActivable
{
    public Pawn? umbral;
    public Thing? mirror;
    public UmbralPactDemand demand;
    public string objectiveText = "";
    public int goal;
    public int progress;
    public int durationTicks;
    // The colonist to bring into the dark, or the relative to kill.
    public Pawn? target;

    public int TicksLeft => State == QuestPartState.Enabled ? enableTick + durationTicks - Find.TickManager.TicksGame : 0;

    public bool Counted => goal > 0;

    // Shown in the mirror's Pacts tab.
    public string StatusLine => Counted ? "BOR_DarkPact_Progress".Translate(progress, goal).Resolve() : objectiveText;

    public override string ExpiryInfoPart => quest.Historical ? null! : "BOR_DarkPact_TimeLeft".Translate(TicksLeft.ToStringTicksToPeriod());

    public override string DescriptionPart => State == QuestPartState.Enabled && Counted ? "BOR_DarkPact_Progress".Translate(progress, goal) : null!;

    public override IEnumerable<GlobalTargetInfo> QuestLookTargets
    {
        get
        {
            if (mirror is { Spawned: true })
            {
                yield return mirror;
            }
            if (umbral != null)
            {
                yield return umbral;
            }
            if (target != null)
            {
                yield return target;
            }
        }
    }

    public override bool QuestPartReserves(Pawn p) => State == QuestPartState.Enabled && (p == umbral || p == target);

    public override void QuestPartTick()
    {
        if (TicksLeft <= 0)
        {
            // The vigil is kept by letting the time run out.
            if (demand == UmbralPactDemand.Vigil)
            {
                Succeed();
            }
            else
            {
                Fail("BOR_DarkPact_Reason_Expired".Translate());
            }
            return;
        }
        if (Find.TickManager.TicksGame % GenTicks.TickLongInterval != 0)
        {
            return;
        }
        // Quest-reserved pawns are never discarded, so an Umbral banished or sold is caught here. A kidnapped one is still ours.
        if (umbral != null && !umbral.Dead && umbral.Faction != Faction.OfPlayer && !umbral.IsKidnapped())
        {
            Fail("BOR_DarkPact_Reason_TargetLost".Translate(umbral.LabelShortCap));
            return;
        }
        if (demand == UmbralPactDemand.Offering)
        {
            UmbralPacts.TryPayOfferings();
        }
        else if ((demand is UmbralPactDemand.IntoTheDark or UmbralPactDemand.SeverTie) && (target == null || (target.Destroyed && !target.Dead)))
        {
            Fail("BOR_DarkPact_Reason_TargetLost".Translate(target?.LabelShortCap ?? "?"));
        }
    }

    public override void Notify_PawnKilled(Pawn pawn, DamageInfo? dinfo)
    {
        if (State != QuestPartState.Enabled)
        {
            return;
        }
        Thing? instigator = dinfo?.Instigator;
        if (pawn == umbral)
        {
            Fail("BOR_DarkPact_Reason_KillerDied".Translate(pawn.LabelShortCap));
            return;
        }
        switch (demand)
        {
            case UmbralPactDemand.Vigil when pawn.RaceProps.Humanlike && umbral != null && instigator == umbral:
                Fail("BOR_UmbralPact_Reason_VigilBroken".Translate(umbral.LabelShortCap));
                break;
            case UmbralPactDemand.SeverTie when pawn == target:
                if (umbral != null && instigator == umbral)
                {
                    Succeed();
                }
                else
                {
                    Fail("BOR_DarkPact_Reason_TargetDiedWrong".Translate(pawn.LabelShortCap));
                }
                break;
            case UmbralPactDemand.IntoTheDark when pawn == target:
                Fail("BOR_UmbralPact_Reason_ConvertDied".Translate(pawn.LabelShortCap));
                break;
        }
    }

    public override void Notify_PawnDiscarded(Pawn pawn)
    {
        if (State == QuestPartState.Enabled && (pawn == umbral || pawn == target))
        {
            Fail("BOR_DarkPact_Reason_TargetLost".Translate(pawn.LabelShortCap));
        }
    }

    public void Count()
    {
        progress++;
        if (progress >= goal)
        {
            Succeed();
        }
    }

    public void Succeed()
    {
        if (State != QuestPartState.Enabled)
        {
            return;
        }
        Complete();
        string granted = "";
        if (umbral is { Dead: false })
        {
            granted = UmbralPacts.GroupLabel(umbral);
            Umbral.StartGroup(umbral);
        }
        Find.LetterStack.ReceiveLetter("BOR_UmbralPact_SuccessLabel".Translate(),
            "BOR_UmbralPact_SuccessText".Translate(umbral?.LabelShortCap ?? "?", granted),
            LetterDefOf.PositiveEvent, umbral is { Spawned: true } ? umbral : LookTargets.Invalid, null, quest);
        quest.End(QuestEndOutcome.Success, sendLetter: false);
    }

    // Every broken pact is punished, abandoned ones too, just like the research pacts.
    public void Fail(string reason)
    {
        if (State != QuestPartState.Enabled)
        {
            return;
        }
        Complete();
        Thing? source = DarkPacts.Punish(mirror);
        string name = umbral?.LabelShortCap ?? "?";
        string text = (source != null ? "BOR_UmbralPact_FailedText" : "BOR_UmbralPact_FailedTextNoMirror").Translate(name, reason);
        Find.LetterStack.ReceiveLetter("BOR_UmbralPact_FailedLabel".Translate(), text, LetterDefOf.NegativeEvent,
            source != null ? source : LookTargets.Invalid, null, quest);
        quest.End(QuestEndOutcome.Fail, sendLetter: false);
    }

    // The quest stays in the history; nothing there needs these, and dead or destroyed ones would not load cleanly.
    public override void Cleanup()
    {
        base.Cleanup();
        umbral = null;
        target = null;
        mirror = null;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref umbral, "umbral");
        Scribe_References.Look(ref mirror, "mirror");
        Scribe_Values.Look(ref demand, "demand");
        Scribe_Values.Look(ref objectiveText, "objectiveText", "");
        Scribe_Values.Look(ref goal, "goal");
        Scribe_Values.Look(ref progress, "progress");
        Scribe_Values.Look(ref durationTicks, "durationTicks");
        Scribe_References.Look(ref target, "target");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            objectiveText ??= "";
        }
    }
}
