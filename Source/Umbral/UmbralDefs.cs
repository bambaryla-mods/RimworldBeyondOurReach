using System.Collections.Generic;
using RimWorld;
using Verse;

namespace BeyondOurReach;

// Marks a building as buildable and usable only by Umbral pawns.
public class DefModExtension_UmbralOnly : DefModExtension
{
}

// Marks Umbral body parts; they cannot be removed by surgery.
public class DefModExtension_UmbralPart : DefModExtension
{
}

public class UmbralPartReplacement
{
    public BodyPartDef bodyPart = null!;
    public HediffDef hediff = null!;
}

// Tunables for the transformation; placed on BOR_UmbralTransformation.
public class DefModExtension_UmbralTransformation : DefModExtension
{
    // Umbral parts one pact grants; the exoskeleton and exoskull come as one pact, the utility pod as another.
    public int partsPerPact = 4;

    public List<UmbralPartReplacement> replacements = new();
}

[DefOf]
public static class BOR_UmbralDefOf
{
    public static HediffDef BOR_Umbral = null!;
    public static HediffDef BOR_UmbralTransformation = null!;
    public static HediffDef BOR_UmbralExoskeleton = null!;
    public static HediffDef BOR_UmbralExoskull = null!;
    public static HediffDef BOR_UmbralCamo = null!;
    public static HediffDef BOR_UmbralUtilityPod = null!;
    public static HediffDef BOR_UmbralBrain = null!;
    public static HediffDef BOR_DarkArchotechCommune = null!;
    public static ThoughtDef BOR_UmbralEmpty = null!;
    public static HediffDef BOR_UmbralHerald = null!;
    public static JobDef BOR_UmbralMurder = null!;
    [DefAlias("BOR_Umbral")]
    public static PawnKindDef BOR_Umbral_Kind = null!;
    public static HediffDef BOR_SlaughterBlessing = null!;
    public static HediffDef BOR_SlaughterRestraint = null!;
    public static FactionDef BOR_Umbrals = null!;
    public static ThingDef BOR_HeraldHarbinger = null!;
    public static ThingDef BOR_HeraldHarbingerIncoming = null!;
    public static ThingDef BOR_HeraldHarbingerLeaving = null!;

    [MayRequireBiotech]
    public static XenotypeDef BOR_UmbralXenotype = null!;

    [MayRequireBiotech]
    public static GeneDef BOR_UmbralNoBodyParts = null!;

    [MayRequireBiotech]
    public static GeneDef BOR_UmbralPsychosisResistance = null!;

    [MayRequireBiotech]
    public static GeneDef BOR_UmbralTransformationGene = null!;

    static BOR_UmbralDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(BOR_UmbralDefOf));
    }
}
