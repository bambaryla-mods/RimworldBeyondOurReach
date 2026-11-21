using System.Reflection;
using HarmonyLib;
using Verse;

namespace BeyondOurReach;

// Private game members looked up by name. If a game update renames one, these return null and log one warning saying
// what stops working, instead of throwing from a static initializer on every later use.
public static class BorReflection
{
    public static AccessTools.FieldRef<T, F>? FieldRef<T, F>(string name, string consequence) where T : class
    {
        if (AccessTools.Field(typeof(T), name) is FieldInfo field)
        {
            return AccessTools.FieldRefAccess<T, F>(field);
        }
        Missing(typeof(T), name, consequence);
        return null;
    }

    public static MethodInfo? Method<T>(string name, string consequence)
    {
        MethodInfo? method = AccessTools.Method(typeof(T), name);
        if (method == null)
        {
            Missing(typeof(T), name, consequence);
        }
        return method;
    }

    private static void Missing(System.Type type, string name, string consequence) =>
        Log.Warning($"[BOR] {type.Name}.{name} is gone after a game update; {consequence}");
}
