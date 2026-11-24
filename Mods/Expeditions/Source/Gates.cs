using Expeditions;

namespace BeyondOurReach;

// Open when the colony's path is `path`, or isn't when `not` is set.
public class SiteGate_Path : SiteGate
{
    public ColonyPath path;
    public bool not;

    public override bool Open() => (GameComponent_OrientisPath.Instance?.Path == path) != not;

    public override string? ClosedReason => not ? $"not on the {path} path" : $"needs the {path} path";
}

// Open while a dark pact is under way with one of these prices. The offer goes when the pact does.
public class SiteGate_PactObjective : SiteGate
{
    public List<DarkPactObjective> objectives = new();

    public override bool Open() => DarkPacts.Active is { } pact && objectives.Contains(pact.objective);

    public override int? DeadlineTick => DarkPacts.Active is { } pact ? Verse.Find.TickManager.TicksGame + pact.TicksLeft : null;

    public override string? ClosedReason => "no dark pact with this price";

    public override IEnumerable<string> ConfigErrors()
    {
        if (objectives.Count == 0) yield return "SiteGate_PactObjective lists no objectives";
    }
}
