using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace BeyondOurReach;

public class DefModExtension_Nuke : DefModExtension
{
    public float radius = 1000f;
    public int damage = 15000;

    // Below 0 uses the damage def's default.
    public float armorPenetration = -1f;
    public DamageDef? damageDef;
    public float chanceToStartFire = 0.1f;
    public float propagationSpeed = 1f;

    // A mountain roof shields what is under it, so mountain bases survive.
    public bool spareThickRoof = true;
    public SoundDef? sound;
    public float screenShakeFactor = 4f;

    // One distortion ring that grows with the front. Its fleck's growthRate must be 80 x propagationSpeed: the front moves
    // 40 cells/s in radius at speed 1, and growthRate is diameter per second.
    public FleckDef? ringFleck;

    // A band of distortion along the front, only where the camera is looking.
    public FleckDef? fringeFleck;
    public FloatRange fringeScale = new(0.8f, 1.4f);
    public float fringeSpacing = 4f;
    public float fringeJitter = 2.5f;
    public int fringeInterval = 4;
    public int fringeMaxPerPulse = 600;

    // Camera rumble while the front is on screen.
    public float fringeShake = 0.3f;

    // Share of the rock removed by the shockwave that leaves its rubble behind.
    public float rubbleChance = 0.15f;
}

// A Cracker shell spawns this at the centre of its own blast, through preExplosionSpawnSingleThingDef, which both
// vanilla and CE explosions honor. Vanilla and CE explosions stop at the radial pattern (about radius 80) and need line of
// sight from the centre, so the shockwave builds its own cell list and hands it to a vanilla explosion as overrideCells.
// It then stays until the front has crossed the map, drawing the distortion at the front.
//
// A rock killed by an explosion costs far more than the damage: a roof collapse search per rock, a yield drop, a history
// event and filth, hundreds of times a tick when the front crosses a mountain. So the shockwave clears its own path
// just ahead of the front: it takes thin roofs off, and removes outright the rock the blast would have destroyed.
public class NukeDetonation : Thing
{
    private static readonly DefModExtension_Nuke Defaults = new();

    // How far ahead of the front rock is removed, so the explosion never reaches a rock first.
    private const float RockLead = 2f;

    // Roofs go further ahead still, so that no roof is left within support range of a rock when it is removed.
    private const float RoofLead = RockLead + RoofCollapseUtility.RoofMaxSupportDistance + 1f;

    // The shockwave's cells, nearest first. Not saved: rebuilt after loading.
    private List<IntVec3>? cells;
    private int roofIndex;
    private int rockIndex;

    private int detonationTick = -1;

    // Distance from the centre to the farthest cell the shockwave reaches.
    private float reach;

    private DefModExtension_Nuke Ext => def.GetModExtension<DefModExtension_Nuke>() ?? Defaults;

    private int TicksSinceDetonation => Find.TickManager.TicksGame - detonationTick;

    // An explosion reaches a cell 1.5 ticks per cell of distance, divided by its propagation speed.
    private float FrontRadius => TicksSinceDetonation * Ext.propagationSpeed / 1.5f;

    protected override void Tick()
    {
        if (!Spawned)
        {
            return;
        }
        if (detonationTick < 0)
        {
            Detonate(Map, Position);
            return;
        }
        if (FrontRadius > reach + Ext.fringeSpacing)
        {
            Destroy();
            return;
        }
        ClearAhead();
        if (Ext.fringeFleck != null && TicksSinceDetonation % Mathf.Max(1, Ext.fringeInterval) == 0)
        {
            DrawFringe();
        }
    }

    private void Detonate(Map map, IntVec3 center)
    {
        DefModExtension_Nuke ext = Ext;
        DamageDef damageDef = ext.damageDef ?? DamageDefOf.Bomb;

        // The shell's own explosion is still standing on this cell; it knows who fired.
        Explosion? source = center.GetThingList(map).OfType<Explosion>().FirstOrDefault();

        cells = BuildCells(map, center);
        reach = cells.Count > 0 ? (cells[cells.Count - 1] - center).LengthHorizontal : 0f;
        detonationTick = Find.TickManager.TicksGame;
        ClearAhead();

        float armorPenetration = ext.armorPenetration >= 0f ? ext.armorPenetration : damageDef.defaultArmorPenetration;
        GenExplosion.DoExplosion(center, map, ext.radius, damageDef, source?.instigator, ext.damage, armorPenetration, ext.sound,
            source?.weapon, source?.projectile, null, chanceToStartFire: ext.chanceToStartFire, damageFalloff: true,
            doVisualEffects: false, propagationSpeed: ext.propagationSpeed, screenShakeFactor: ext.screenShakeFactor, overrideCells: cells);

        if (ext.ringFleck != null)
        {
            FleckCreationData ring = FleckMaker.GetDataStatic(center.ToVector3Shifted(), map, ext.ringFleck, 0.01f);
            ring.solidTimeOverride = reach * 1.5f / Mathf.Max(0.01f, ext.propagationSpeed) / 60f;
            map.flecks.CreateFleck(ring);
        }
    }

    private List<IntVec3> BuildCells(Map map, IntVec3 center)
    {
        DefModExtension_Nuke ext = Ext;
        float radiusSquared = ext.radius * ext.radius;
        List<IntVec3> result = new();
        foreach (IntVec3 cell in map.AllCells)
        {
            if ((cell - center).LengthHorizontalSquared > radiusSquared)
            {
                continue;
            }
            if (ext.spareThickRoof && (cell.GetRoof(map)?.isThickRoof ?? false))
            {
                continue;
            }
            result.Add(cell);
        }
        result.Sort((a, b) => (a - center).LengthHorizontalSquared.CompareTo((b - center).LengthHorizontalSquared));
        return result;
    }

    private void ClearAhead()
    {
        Map map = Map;
        IntVec3 center = Position;
        DefModExtension_Nuke ext = Ext;

        // After loading, the cells behind the front are walked once more; there is nothing left on them to clear.
        cells ??= BuildCells(map, center);

        // The explosion reaches a cell up to one tick of travel beyond FrontRadius.
        float front = FrontRadius + ext.propagationSpeed;

        float roofFront = front + RoofLead;
        for (; roofIndex < cells.Count; roofIndex++)
        {
            IntVec3 cell = cells[roofIndex];
            if ((cell - center).LengthHorizontal > roofFront)
            {
                break;
            }
            map.roofGrid.SetRoof(cell, null);
        }

        float rockFront = front + RockLead;
        for (; rockIndex < cells.Count; rockIndex++)
        {
            IntVec3 cell = cells[rockIndex];
            float distance = (cell - center).LengthHorizontal;
            if (distance > rockFront)
            {
                break;
            }

            // Only rock the blast would destroy anyway; anything tougher is left to the explosion.
            if (cell.GetEdifice(map) is not Mineable rock || rock.HitPoints > Mathf.Lerp(ext.damage, ext.damage * 0.2f, distance / ext.radius))
            {
                continue;
            }
            ThingDef? rubble = rock.def.filthLeaving;
            rock.Destroy(DestroyMode.Vanish);
            if (rubble != null && Rand.Chance(ext.rubbleChance))
            {
                FilthMaker.TryMakeFilth(cell, map, rubble);
            }
        }
    }

    private void DrawFringe()
    {
        DefModExtension_Nuke ext = Ext;
        float radius = FrontRadius;
        if (radius < 1f || Map != Find.CurrentMap || WorldRendererUtility.WorldRendered)
        {
            return;
        }
        CellRect view = Find.CameraDriver.CurrentViewRect.ExpandedBy(4);
        Vector3 center = Position.ToVector3Shifted();
        int count = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.PI * radius / Mathf.Max(0.5f, ext.fringeSpacing)), 8, ext.fringeMaxPerPulse);
        float step = 2f * Mathf.PI / count;
        float offset = Rand.Range(0f, step);
        bool onScreen = false;
        for (int i = 0; i < count; i++)
        {
            float angle = offset + i * step;
            float distance = radius + Rand.Range(-ext.fringeJitter, ext.fringeJitter);
            Vector3 point = center + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            IntVec3 cell = point.ToIntVec3();
            if (!view.Contains(cell) || !cell.InBounds(Map))
            {
                continue;
            }
            FleckCreationData data = FleckMaker.GetDataStatic(point, Map, ext.fringeFleck, ext.fringeScale.RandomInRange);
            data.rotation = Rand.Range(0f, 360f);
            Map.flecks.CreateFleck(data);
            onScreen = true;
        }
        if (onScreen && ext.fringeShake > 0f)
        {
            Find.CameraDriver.shaker.DoShake(ext.fringeShake);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref detonationTick, "detonationTick", -1);
        Scribe_Values.Look(ref reach, "reach", 0f);
    }
}
