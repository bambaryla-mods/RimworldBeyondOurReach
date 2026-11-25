using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BeyondOurReach;

// Marks one of our research projects whose cost is only a placeholder (for example a dark pact, finished by a quest and never at a bench).
// The research tab and the project tooltip show a text instead of the cost. Only the text changes.
public class DefModExtension_InfiniteCostDisplay : DefModExtension
{
    // Translation key of the text shown in place of the cost.
    public string textKey = "BOR_PactCostDisplay";
}

// Shared by the vanilla, YART and Nice Research Tab patches below. Each of them rewrites the code that turns a research cost into text.
// A cost read (or Nice Research Tab's cached cost) of a marked project leaves its text in pendingText; the formatting call that follows picks it up.
// Patched via the assembly-wide PatchAll in SettingsWindowPatch.
public static class ResearchCostDisplay
{
    private const int FormatWindow = 6;

    private static readonly MethodInfo CostApparent = AccessTools.PropertyGetter(typeof(ResearchProjectDef), nameof(ResearchProjectDef.CostApparent));
    private static readonly MethodInfo Cost = AccessTools.PropertyGetter(typeof(ResearchProjectDef), nameof(ResearchProjectDef.Cost));
    private static readonly MethodInfo FloatToStringFormat = AccessTools.Method(typeof(float), nameof(float.ToString), new[] { typeof(string) });
    private static readonly MethodInfo FloatToString = AccessTools.Method(typeof(float), nameof(float.ToString), Type.EmptyTypes);
    private static readonly MethodInfo IntToString = AccessTools.Method(typeof(int), nameof(int.ToString), Type.EmptyTypes);
    private static readonly MethodInfo AppendFloatFormat = AppendFormatted(typeof(float), 2);
    private static readonly MethodInfo AppendInt = AppendFormatted(typeof(int), 1);

    private static readonly MethodInfo ShownCostApparent = AccessTools.Method(typeof(ResearchCostDisplay), nameof(ShownCostApparentOf));
    private static readonly MethodInfo ShownCost = AccessTools.Method(typeof(ResearchCostDisplay), nameof(ShownCostOf));
    private static readonly MethodInfo MarkNode = AccessTools.Method(typeof(ResearchCostDisplay), nameof(Mark));
    private static readonly MethodInfo FloatWithFormat = AccessTools.Method(typeof(ResearchCostDisplay), nameof(FormatCost), new[] { typeof(float).MakeByRefType(), typeof(string) });
    private static readonly MethodInfo FloatPlain = AccessTools.Method(typeof(ResearchCostDisplay), nameof(FormatCost), new[] { typeof(float).MakeByRefType() });
    private static readonly MethodInfo IntPlain = AccessTools.Method(typeof(ResearchCostDisplay), nameof(FormatCost), new[] { typeof(int).MakeByRefType() });
    private static readonly MethodInfo AppendFloatReplacement = AccessTools.Method(typeof(ResearchCostDisplay), nameof(AppendCost), new[] { typeof(DefaultInterpolatedStringHandler).MakeByRefType(), typeof(float), typeof(string) });
    private static readonly MethodInfo AppendIntReplacement = AccessTools.Method(typeof(ResearchCostDisplay), nameof(AppendCost), new[] { typeof(DefaultInterpolatedStringHandler).MakeByRefType(), typeof(int) });

    // Set by a cost read just before the text is built, which happens on the main thread within a few instructions.
    private static string? pendingText;

    private static MethodInfo AppendFormatted(Type argument, int parameterCount) =>
        typeof(DefaultInterpolatedStringHandler).GetMethods()
            .First(m => m.Name == nameof(DefaultInterpolatedStringHandler.AppendFormatted) && m.IsGenericMethodDefinition && m.GetParameters().Length == parameterCount
                        && m.GetParameters()[0].ParameterType == m.GetGenericArguments()[0] && (parameterCount == 1 || m.GetParameters()[1].ParameterType == typeof(string)))
            .MakeGenericMethod(argument);

    // Flagged projects report an infinite cost to the text code; everything else gets the real cost.
    public static float ShownCostApparentOf(ResearchProjectDef project) => Shown(project, project.CostApparent);

    public static float ShownCostOf(ResearchProjectDef project) => Shown(project, project.Cost);

    private static float Shown(ResearchProjectDef project, float realCost)
    {
        pendingText = TextFor(project);
        return pendingText == null ? realCost : float.PositiveInfinity;
    }

    private static string? TextFor(ResearchProjectDef? project) =>
        project?.GetModExtension<DefModExtension_InfiniteCostDisplay>()?.textKey.Translate();

    // For Nice Research Tab, whose research node keeps its cost as an int. The node is any object with a Project field or property.
    public static void Mark(object node)
    {
        MemberInfo? member = (MemberInfo?)node.GetType().GetField("Project") ?? node.GetType().GetProperty("Project");
        pendingText = TextFor(member switch
        {
            FieldInfo field => field.GetValue(node) as ResearchProjectDef,
            PropertyInfo property => property.GetValue(node) as ResearchProjectDef,
            _ => null,
        });
    }

    public static string FormatCost(ref float cost, string format) => TakeText() ?? cost.ToString(format);

    public static string FormatCost(ref float cost) => TakeText() ?? cost.ToString();

    public static string FormatCost(ref int cost) => TakeText() ?? cost.ToString();

    public static void AppendCost(ref DefaultInterpolatedStringHandler handler, float cost, string format)
    {
        string? text = TakeText();
        if (text != null)
        {
            handler.AppendFormatted(text);
        }
        else
        {
            handler.AppendFormatted(cost, format);
        }
    }

    public static void AppendCost(ref DefaultInterpolatedStringHandler handler, int cost)
    {
        string? text = TakeText();
        if (text != null)
        {
            handler.AppendFormatted(text);
        }
        else
        {
            handler.AppendFormatted(cost);
        }
    }

    private static string? TakeText()
    {
        string? text = pendingText;
        pendingText = null;
        return text;
    }

    // The three mods all draw the cost with different code, so one transpiler rewrites every shape:
    //   cost read (or Nice Research Tab's CachedCost) ... float.ToString / int.ToString / DefaultInterpolatedStringHandler.AppendFormatted
    // Both ends of a pair are swapped together. Other reads of the cost, such as math, are left alone.
    public static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = new(instructions);
        List<int> markBefore = new();
        for (int i = 0; i < code.Count; i++)
        {
            bool isGetter = code[i].Calls(CostApparent) || code[i].Calls(Cost);
            bool isCachedCost = (code[i].opcode == OpCodes.Ldfld || code[i].opcode == OpCodes.Ldflda) && code[i].operand is FieldInfo { Name: "CachedCost" };
            if (!isGetter && !isCachedCost)
            {
                continue;
            }
            for (int j = i + 1; j < code.Count && j <= i + FormatWindow; j++)
            {
                MethodInfo? consumer = ConsumerFor(code[j], isGetter);
                if (consumer == null)
                {
                    continue;
                }
                if (isGetter)
                {
                    code[i].opcode = OpCodes.Call;
                    code[i].operand = code[i].Calls(CostApparent) ? ShownCostApparent : ShownCost;
                }
                else
                {
                    markBefore.Add(i);
                }
                code[j].opcode = OpCodes.Call;
                code[j].operand = consumer;
                break;
            }
        }
        // The node is already on the stack before the field load: copy it and hand the copy to Mark.
        foreach (int index in Enumerable.Reverse(markBefore))
        {
            CodeInstruction dup = new(OpCodes.Dup);
            dup.MoveLabelsFrom(code[index]);
            code.InsertRange(index, new[] { dup, new CodeInstruction(OpCodes.Call, MarkNode) });
        }
        return code;
    }

    private static MethodInfo? ConsumerFor(CodeInstruction instruction, bool fromGetter)
    {
        if (fromGetter)
        {
            if (instruction.Calls(FloatToStringFormat)) return FloatWithFormat;
            if (instruction.Calls(FloatToString)) return FloatPlain;
            if (instruction.Calls(AppendFloatFormat)) return AppendFloatReplacement;
            // Nice Research Tab's queue casts the cost to an int before printing it.
            if (instruction.Calls(AppendInt)) return AppendIntReplacement;
            return null;
        }
        if (instruction.Calls(IntToString)) return IntPlain;
        if (instruction.Calls(AppendInt)) return AppendIntReplacement;
        return null;
    }
}

// Vanilla research tab: the progress line, the bottom row and the project tooltip.
[HarmonyPatch]
public static class ResearchCostDisplayPatches
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodInfo method in AccessTools.GetDeclaredMethods(typeof(MainTabWindow_Research)))
        {
            yield return method;
        }
        yield return AccessTools.Method(typeof(ResearchProjectDef), nameof(ResearchProjectDef.GetTip));
    }

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => ResearchCostDisplay.Rewrite(instructions);
}

// Yet Another Research Tree (seohyeon.yart): only applied when it is loaded.
[HarmonyPatch]
public static class ResearchCostDisplayYartPatches
{
    private static readonly Type? YartWindow = AccessTools.TypeByName("YART.MainTabWindow_YART");

    static bool Prepare() => YartWindow != null;

    static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredMethods(YartWindow).Where(m => !m.IsAbstract && !m.IsGenericMethodDefinition);

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => ResearchCostDisplay.Rewrite(instructions);
}

// Nice Research Tab (Andromeda.NiceResearchTab): only applied when it is loaded.
[HarmonyPatch]
public static class ResearchCostDisplayNiceTabPatches
{
    private static readonly Type?[] Nodes =
    {
        AccessTools.TypeByName("NiceResearchTab.ResearchNode"),
        AccessTools.TypeByName("NiceResearchTab.ResearchNodeQueued"),
    };

    static bool Prepare() => Nodes.Any(t => t != null);

    static IEnumerable<MethodBase> TargetMethods() =>
        Nodes.OfType<Type>().SelectMany(t => AccessTools.GetDeclaredMethods(t)).Where(m => !m.IsAbstract && !m.IsGenericMethodDefinition);

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => ResearchCostDisplay.Rewrite(instructions);
}
