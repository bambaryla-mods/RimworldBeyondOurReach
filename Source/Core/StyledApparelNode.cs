using HarmonyLib;
using Verse;

namespace BeyondOurReach;

/// <summary>
/// Worn texture for a styled apparel item drawn by its own render node (such as Combat Extended shields). Vanilla's
/// wornGraphicPath does not reach those nodes: they read the texPath set in the apparel's renderNodeProperties.
/// </summary>
public class DefModExtension_StyledRenderNode : DefModExtension
{
    public string? texPath;
}

// Patched via the assembly-wide PatchAll in SettingsWindowPatch.
// Vanilla apparel nodes build their graphic elsewhere and never call TexPathFor; CE's apparel node does.
[HarmonyPatch(typeof(PawnRenderNode), "TexPathFor")]
public static class PawnRenderNode_TexPathFor_StyledApparel_Patch
{
    public static void Postfix(PawnRenderNode __instance, ref string __result)
    {
        string? path = __instance.apparel?.StyleDef?.GetModExtension<DefModExtension_StyledRenderNode>()?.texPath;
        if (!path.NullOrEmpty())
        {
            __result = path!;
        }
    }
}
