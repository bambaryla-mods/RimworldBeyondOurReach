using Expeditions;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace BeyondOurReach;

// Days among tenebrite: each day on site counts as standing near tenebrite at this emission, through the colony's own
// psychosis rules. Ordinary pawns come back latent and it fades; tenebral pawns start from their floor and can tip.
public class PawnEffect_TenebriteExposure : PawnEffect
{
    public float emission = 3f;

    public override void Apply(Pawn p, Expedition e, Outcome outcome, ExpeditionReport report) =>
        TenebralPsychosis.UpdatePawn(p, e.opp.site.daysOnSite, emission);
}

// A full blessing that never fades, on one blessable part, for an Orientis pawn that has none yet.
public class PawnEffect_TouchOfTheHoly : PawnEffect
{
    public float chance = 0.1f;

    public PawnEffect_TouchOfTheHoly() => outcomes = new() { Outcome.Success };

    public override void Apply(Pawn p, Expedition e, Outcome outcome, ExpeditionReport report)
    {
        if (Blessings.Touch is not { } touch || !Orientis.IsOrientis(p) || Blessings.HasTouch(p) || !Rand.Chance(chance))
        {
            return;
        }
        List<BodyPartRecord> parts = Blessings.Blessable(p, Blessings.BenchParts).ToList();
        if (parts.Count == 0)
        {
            return;
        }
        BodyPartRecord part = parts.RandomElement();
        Hediff? old = p.health.hediffSet.hediffs.FirstOrDefault(h => h.def == BOR_OrientisDefOf.BOR_Blessing && h.Part == part);
        if (old != null)
        {
            p.health.RemoveHediff(old);
        }
        p.health.AddHediff(touch, part);
        report.effectLines.Add("BOR_Exp_TouchLine".Translate(p.LabelShortCap, part.Label));
    }
}

// A hunt for people. Bloodlust is sated; kills count toward crystallization by the rules a kill at home follows, but
// never so many that one trip leaves a brain fully crystallized. Every kill counts toward a "kill people" pact.
public class PawnEffect_Reaping : PawnEffect
{
    public IntRange kills = new(2, 5);

    public PawnEffect_Reaping() => outcomes = new() { Outcome.Success, Outcome.Partial };

    public override void Apply(Pawn p, Expedition e, Outcome outcome, ExpeditionReport report)
    {
        int rolled = kills.RandomInRange;
        if (p.needs?.TryGetNeed<Need_Murder>() is { } need)
        {
            need.CurLevel = 1f;
        }
        if (TenebralPsychosis.CountsKill(p))
        {
            Hediff_TenebralBreaks counter = TenebralPsychosis.BreakCounter(p);
            int counted = Capped(counter.kills, rolled, Hediff_TenebralBreaks.KillsToPod(p), TenebralPsychosis.BreakSettings.podThreshold);
            for (int i = 0; i < counted; i++)
            {
                counter.Notify_Kill();
            }
        }
        if (DarkPacts.Active is { objective: DarkPactObjective.KillPeople } pact)
        {
            pact.AddProgress(rolled);
        }
        report.effectLines.Add("BOR_Exp_ReapingLine".Translate(p.LabelShortCap, rolled));
    }

    // The most of `add` kills that keeps crystallization (podThreshold x kills / killsToPod) below a full brain.
    // Same rule as Expeditions' HookMath.CappedKills, whose tests cover it.
    public static int Capped(int have, int add, int killsToPod, float podThreshold)
    {
        if (add <= 0 || killsToPod <= 0 || podThreshold <= 0f)
        {
            return System.Math.Max(0, add);
        }
        int maxTotal = (int)System.Math.Ceiling(killsToPod / (double)podThreshold) - 1;
        return System.Math.Max(0, System.Math.Min(add, maxTotal - have));
    }
}

// Meditation power banked into the caches of the map the squad comes home to. What doesn't fit is lost.
public class OutcomeEffect_MeditationPower : OutcomeEffect
{
    public float amount = 600f;

    public OutcomeEffect_MeditationPower() => outcomes = new() { Outcome.Success, Outcome.Partial };

    public override void OnArrival(Expedition e, Outcome outcome, Map? map, ExpeditionReport report)
    {
        float give = outcome == Outcome.Partial ? amount / 2f : amount;
        MapComponent_MeditationPower? power = map != null ? MapComponent_MeditationPower.Of(map) : null;
        if (power == null)
        {
            report.effectLines.Add("BOR_Exp_PowerFaded".Translate());
            return;
        }
        float before = power.Stored;
        power.Add(give);
        float banked = power.Stored - before;
        report.effectLines.Add(banked > 0.5f ? "BOR_Exp_PowerBanked".Translate(banked.ToString("F0")) : "BOR_Exp_PowerFaded".Translate());
    }
}

public class ScripPerThing
{
    public ThingDef thing = null!;
    public float scrip;
}

// Scrip from the Kwazaari: a flat sum, plus a price per matching thing that actually came home.
public class OutcomeEffect_Scrip : OutcomeEffect
{
    public FloatRange amount = FloatRange.Zero;
    public List<ScripPerThing> perThing = new();

    public OutcomeEffect_Scrip() => outcomes = new() { Outcome.Success, Outcome.Partial };

    public override void OnArrival(Expedition e, Outcome outcome, Map? map, ExpeditionReport report)
    {
        float total = amount.RandomInRange * (outcome == Outcome.Partial ? 0.5f : 1f);
        foreach (ScripPerThing per in perThing.Where(x => x.thing != null))
        {
            total += per.scrip * e.contents.Where(t => t.def == per.thing).Sum(t => t.stackCount);
        }
        if (total <= 0.001f || GameComponent_Kwazaari.Instance is not { } kwazaari)
        {
            return;
        }
        kwazaari.AddScrip(total);
        report.effectLines.Add("BOR_Exp_ScripLine".Translate(total.ToString("0.##")));
    }
}

// Pays the dark pact under way: all that is still owed on a win, half of it on a partial win. Only a pact with one of
// these prices counts: a squad sent for one pact never pays a different one made while it was away.
public class OutcomeEffect_PactProgress : OutcomeEffect
{
    public List<DarkPactObjective> objectives = new();

    public OutcomeEffect_PactProgress() => outcomes = new() { Outcome.Success, Outcome.Partial };

    public override IEnumerable<string> ConfigErrors()
    {
        if (objectives.Count == 0) yield return "OutcomeEffect_PactProgress lists no objectives";
    }

    public override void Apply(Expedition e, Outcome outcome, ExpeditionReport report)
    {
        if (DarkPacts.Active is not { } pact || !pact.Counted || !objectives.Contains(pact.objective))
        {
            report.effectLines.Add("BOR_Exp_PactGone".Translate());
            return;
        }
        int owed = pact.Remaining;
        int paid = outcome == Outcome.Success ? owed : (owed + 1) / 2;
        pact.AddProgress(paid);
        report.effectLines.Add("BOR_Exp_PactPaid".Translate(paid));
    }
}

// The settlement a dark pact asks the colony to destroy, and only that one. The squad turns back if the pact ends.
public class SettlementLinkWorker_Pact : SettlementLinkWorker
{
    private static Settlement? PactSettlement =>
        DarkPacts.Active is { objective: DarkPactObjective.DestroySettlement } pact ? pact.settlement : null;

    public override bool CanOffer(Settlement s) => s == PactSettlement;

    public override bool StillWanted(Opportunity o) => o.linkedSettlement != null && o.linkedSettlement == PactSettlement;
}
