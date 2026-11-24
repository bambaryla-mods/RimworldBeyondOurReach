using Expeditions;
using Verse;

namespace BeyondOurReach;

// On the Path of Light nothing dark comes home: tenebrite, anything made with it, and tenebral or Umbral things burn
// where they lie, by the same test that keeps Orientis pawns from touching them.
public class BORLootFilter : ExpeditionLootFilter
{
    public override string? Filter(Expedition e, List<Thing> loot)
    {
        if (GameComponent_OrientisPath.Instance?.Path != ColonyPath.Light)
        {
            return null;
        }
        int removed = loot.RemoveAll(t => OrientisRestrictions.IsDarkThing(t) || TenebralPsychosis.MadeOfTenebrite(t)
            || t.def == BOR_TenebralDefOf.BOR_Tenebrite);
        return removed > 0 ? "BOR_Exp_DarkBurned".Translate() : null;
    }
}
