using UnityEngine;
using Verse;

namespace BeyondOurReach;

// The dark mirror's portal: floats and bobs over the base, phases from off to on through the BOR_MirrorPhase shader
// while the pod holds a mode, and glows while someone is inside. Every texture has the building's draw size.
// The building needs drawerType MapMeshAndRealTime.
public class CompProperties_MirrorPortal : CompProperties
{
    public string offTexPath = "";
    public string onTexPath = "";
    public string glowTexPath = "";
    // How far the portal rises and falls, in cells, and how long one bob takes.
    public float bobAmplitude = 0.06f;
    public int bobPeriodTicks = 240;
    // How long a full phase from off to on (or back) takes.
    public int phaseTicks = 60;
    // Width of the glowing edge, in noise units, and how many noise cells span the texture.
    public float edgeWidth = 0.08f;
    public float noiseScale = 24f;
    public Color edgeColor = new(1f, 0.25f, 0.2f, 1f);
    // The glow pulses between full and (1 - glowPulseStrength) over glowPulseTicks.
    public int glowPulseTicks = 240;
    public float glowPulseStrength = 0.5f;

    public CompProperties_MirrorPortal()
    {
        compClass = typeof(CompMirrorPortal);
    }
}

public sealed class CompMirrorPortal : ThingComp
{
    private const float PortalHeight = 0.012f;
    private const float GlowHeight = 0.013f;
    private const int PortalQueue = 2960;
    private const int GlowQueue = 2961;

    // Each mirror phases on its own, so each has its own material.
    private Material? portalMaterial;
    private Material? glowMaterial;

    // Not saved: after a load the phase starts settled at its target.
    private bool initialized;
    private bool lastInMode;
    private float phaseAtChange;
    private int changeTick;

    private CompProperties_MirrorPortal Props => (CompProperties_MirrorPortal)props;

    private Building_TenebralPod Pod => (Building_TenebralPod)parent;

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map, mode);
        if (portalMaterial != null)
        {
            UnityEngine.Object.Destroy(portalMaterial);
            portalMaterial = null;
        }
        initialized = false;
    }

    private float Phase(int now, bool inMode)
    {
        if (!initialized)
        {
            initialized = true;
            lastInMode = inMode;
            phaseAtChange = inMode ? 1f : 0f;
            changeTick = now;
        }
        else if (inMode != lastInMode)
        {
            // Turning back mid-phase continues from where the phase is now.
            phaseAtChange = CurrentPhase(now);
            changeTick = now;
            lastInMode = inMode;
        }
        return CurrentPhase(now);
    }

    private float CurrentPhase(int now)
    {
        float moved = (float)(now - changeTick) / Mathf.Max(1, Props.phaseTicks);
        return Mathf.Clamp01(lastInMode ? phaseAtChange + moved : phaseAtChange - moved);
    }

    public override void PostDraw()
    {
        base.PostDraw();
        int now = Find.TickManager.TicksGame;
        float phase = Phase(now, Pod.InMode);

        if (portalMaterial == null)
        {
            portalMaterial = new Material(DefDatabase<ShaderDef>.GetNamed("BOR_MirrorPhase").Shader)
            {
                mainTexture = ContentFinder<Texture2D>.Get(Props.offTexPath)
            };
            portalMaterial.SetTexture("_OnTex", ContentFinder<Texture2D>.Get(Props.onTexPath));
            portalMaterial.SetColor("_EdgeColor", Props.edgeColor);
        }
        portalMaterial.SetVector("_Phase", new Vector4(phase, Props.edgeWidth, Props.noiseScale, 0f));

        float bob = Props.bobAmplitude * Mathf.Sin(now * 2f * Mathf.PI / Props.bobPeriodTicks);
        Vector3 center = parent.DrawPos + parent.Graphic.DrawOffset(parent.Rotation) + new Vector3(0f, 0f, bob);
        Vector2 size = parent.Graphic.drawSize;
        Vector3 scale = new(size.x, 1f, size.y);
        Matrix4x4 portal = Matrix4x4.TRS(center + new Vector3(0f, PortalHeight, 0f), Quaternion.identity, scale);
        Graphics.DrawMesh(MeshPool.plane10, portal, portalMaterial, 0, null!, 0, (MaterialPropertyBlock)null!,
            UnityEngine.Rendering.ShadowCastingMode.Off, false, PortalQueue);

        if (!Pod.InMode || Pod.Occupant == null)
        {
            return;
        }
        glowMaterial ??= MaterialPool.MatFrom(Props.glowTexPath, ShaderDatabase.MoteGlow);
        float pulse = 0.5f + 0.5f * Mathf.Sin(now * 2f * Mathf.PI / Props.glowPulseTicks);
        Material faded = FadedMaterialPool.FadedVersionOf(glowMaterial, 1f - Props.glowPulseStrength * (1f - pulse));
        Matrix4x4 glow = Matrix4x4.TRS(center + new Vector3(0f, GlowHeight, 0f), Quaternion.identity, scale);
        Graphics.DrawMesh(MeshPool.plane10, glow, faded, 0, null!, 0, (MaterialPropertyBlock)null!,
            UnityEngine.Rendering.ShadowCastingMode.Off, false, GlowQueue);
    }
}
