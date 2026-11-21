using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BeyondOurReach;

// The dark mirror's Pacts tab: the pact under way, then every tenebral research, ready ones first.
public class ITab_DarkPacts : BorTab
{
    private const float Pad = 10f;
    private const float Gap = 6f;
    private const float RowHeight = 52f;
    private const float IconSize = 40f;
    private const float RightColumn = 170f;
    private const float ButtonHeight = 28f;

    private const float HeaderHeight = 26f;
    private const int VisibleUmbralRows = 3;

    private Vector2 scroll;
    private Vector2 umbralScroll;

    public ITab_DarkPacts()
    {
        labelKey = "BOR_TabDarkPacts";
        size = new Vector2(480f, 540f);
    }

    private CompDarkPact? Mirror => SelThing?.TryGetComp<CompDarkPact>();

    public override bool IsVisible => Mirror != null && SelThing!.Faction == Faction.OfPlayer;

    protected override void UpdateSize()
    {
        List<Pawn>? umbrals = Mirror != null ? UmbralRows(Mirror) : null;
        float umbralHeight = umbrals is { Count: > 0 } ? UmbralSectionHeight(umbrals) + Gap * 2f : 0f;
        size = new Vector2(480f, 540f + TitleHeight + umbralHeight);
        base.UpdateSize();
    }

    protected override void Contents(Rect rect)
    {
        CompDarkPact? mirror = Mirror;
        if (mirror == null)
        {
            return;
        }
        Rect inner = rect.ContractedBy(Pad);
        float y = inner.y + DrawActive(new Rect(inner.x, inner.y, inner.width, 0f)) + Gap * 2f;
        List<Pawn> umbrals = UmbralRows(mirror);
        if (umbrals.Count > 0)
        {
            float height = UmbralSectionHeight(umbrals);
            DrawUmbrals(new Rect(inner.x, y, inner.width, height), umbrals, mirror);
            y += height + Gap * 2f;
        }
        DrawList(new Rect(inner.x, y, inner.width, inner.yMax - y), mirror);
    }

    // The pact under way, or a line saying there is none. Returns the height used.
    private static float DrawActive(Rect top)
    {
        BorSkin skin = BorSkin.Current;
        QuestPart_DarkPact? active = DarkPacts.Active;
        Text.Font = GameFont.Small;
        if (active == null)
        {
            string idle = "BOR_DarkPact_TabIdle".Translate();
            float h = Text.CalcHeight(idle, top.width);
            GUI.color = skin.TextDim;
            Widgets.Label(new Rect(top.x, top.y, top.width, h), idle);
            GUI.color = Color.white;
            return h;
        }

        float textWidth = top.width - Pad * 2f;
        float objectiveHeight = Text.CalcHeight(active.objectiveText, textWidth);
        bool dev = DebugSettings.ShowDevGizmos;
        float height = Pad + 24f + objectiveHeight + Gap + (active.Counted ? 24f + Gap : 0f) + ButtonHeight + (dev ? Gap + ButtonHeight : 0f) + Pad;
        Rect box = new(top.x, top.y, top.width, height);
        skin.Section(box);
        float x = box.x + Pad;
        float y = box.y + Pad;

        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(x, y - 4f, textWidth, 30f), "BOR_DarkPact_TabActive".Translate(active.research?.LabelCap ?? ""));
        Text.Font = GameFont.Small;
        y += 24f;
        Widgets.Label(new Rect(x, y, textWidth, objectiveHeight), active.objectiveText);
        y += objectiveHeight + Gap;
        if (active.Counted)
        {
            skin.Bar(new Rect(x, y, textWidth, 24f), Mathf.Clamp01((float)active.Current / active.goal),
                "BOR_DarkPact_Progress".Translate(active.Current, active.goal), BorTone.Bad);
            y += 24f + Gap;
        }

        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = active.TicksLeft < GenDate.TicksPerDay ? skin.TextBad : skin.TextWarn;
        Widgets.Label(new Rect(x, y, textWidth - RightColumn, ButtonHeight), "BOR_DarkPact_TimeLeft".Translate(active.TicksLeft.ToStringTicksToPeriod()).Resolve().StripTags());
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Rect abandon = new(x + textWidth - RightColumn, y, RightColumn, ButtonHeight);
        if (skin.Button(abandon, "BOR_DarkPact_Abandon".Translate(), tone: BorTone.Bad))
        {
            CompDarkPact.ConfirmAbandon();
        }
        TooltipHandler.TipRegion(abandon, "BOR_DarkPact_AbandonDesc".Translate());
        y += ButtonHeight;

        if (dev)
        {
            y += Gap;
            float half = (textWidth - Gap) / 2f;
            if (skin.Button(new Rect(x, y, half, ButtonHeight), "DEV: Complete pact"))
            {
                active.Succeed();
            }
            if (skin.Button(new Rect(x + half + Gap, y, half, ButtonHeight), "DEV: Expire pact"))
            {
                active.Fail("BOR_DarkPact_Reason_Expired".Translate());
            }
        }
        return height;
    }

    private void DrawList(Rect area, CompDarkPact mirror)
    {
        BorSkin skin = BorSkin.Current;
        // Ready first, then held back for the moment, then locked by research, then granted; within each, research order.
        List<(ResearchProjectDef research, string? blocked)> rows = DarkPacts.All
            .Select(r => (research: r, blocked: r.IsFinished ? null : mirror.BlockReason(r)))
            .OrderBy(e => e.research.IsFinished ? 3 : e.blocked == null ? 0 : e.research.PrerequisitesCompleted ? 1 : 2)
            .ToList();

        skin.Section(area);
        Rect outer = area.ContractedBy(4f);
        Rect view = new(0f, 0f, outer.width - 16f, rows.Count * RowHeight);
        Widgets.BeginScrollView(outer, ref scroll, view);
        for (int i = 0; i < rows.Count; i++)
        {
            DrawRow(new Rect(0f, i * RowHeight, view.width, RowHeight), rows[i].research, rows[i].blocked, mirror, i);
        }
        Widgets.EndScrollView();
    }

    // Colony Umbrals on the mirror's map that can transform, and every Umbral with a pact under way wherever it is.
    private static List<Pawn> UmbralRows(CompDarkPact mirror)
    {
        IEnumerable<Pawn> here = mirror.parent.Map?.mapPawns.FreeColonistsSpawned.Where(p => Umbral.IsUmbral(p) && Umbral.Transforms(p)) ?? Enumerable.Empty<Pawn>();
        IEnumerable<Pawn> bound = UmbralPacts.Active.Select(p => p.umbral).OfType<Pawn>();
        return here.Concat(bound).Distinct().OrderBy(p => p.LabelShort).ToList();
    }

    // A running pact's row carries the dev buttons under its Abandon button.
    private static float UmbralRowHeight(Pawn umbral) =>
        RowHeight + (DebugSettings.ShowDevGizmos && UmbralPacts.For(umbral) != null ? ButtonHeight + Gap : 0f);

    private static float UmbralSectionHeight(List<Pawn> umbrals) =>
        HeaderHeight + umbrals.Take(VisibleUmbralRows).Sum(UmbralRowHeight) + 8f;

    private void DrawUmbrals(Rect area, List<Pawn> umbrals, CompDarkPact mirror)
    {
        BorSkin skin = BorSkin.Current;
        skin.Section(area);
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(area.x + Pad, area.y + 4f, area.width - Pad * 2f, HeaderHeight), "BOR_UmbralPact_Header".Translate());
        Rect outer = new(area.x + 4f, area.y + HeaderHeight, area.width - 8f, area.height - HeaderHeight - 4f);
        float total = umbrals.Sum(UmbralRowHeight);
        bool scrolls = total > outer.height + 0.5f;
        Rect view = new(0f, 0f, outer.width - (scrolls ? 16f : 0f), total);
        Widgets.BeginScrollView(outer, ref umbralScroll, view, scrolls);
        float y = 0f;
        for (int i = 0; i < umbrals.Count; i++)
        {
            float h = UmbralRowHeight(umbrals[i]);
            DrawUmbralRow(new Rect(0f, y, view.width, h), umbrals[i], mirror, i);
            y += h;
        }
        Widgets.EndScrollView();
    }

    private static void DrawUmbralRow(Rect row, Pawn umbral, CompDarkPact mirror, int index)
    {
        BorSkin skin = BorSkin.Current;
        QuestPart_UmbralPact? pact = UmbralPacts.For(umbral);
        bool done = pact == null && Umbral.GroupSteps(umbral) == 0;
        skin.Row(row, false, hoverable: true, alternate: index % 2 == 1);

        Rect icon = new(row.x + Gap, row.y + (RowHeight - IconSize) / 2f, IconSize, IconSize);
        Widgets.ThingIcon(icon, umbral);

        float textX = icon.xMax + Gap * 2f;
        float right = row.xMax - RightColumn - Gap;
        Rect name = new(textX, row.y + 6f, right - textX, 22f);
        Text.Font = GameFont.Small;
        Widgets.Label(name, umbral.LabelShortCap.Truncate(name.width));
        Text.Font = GameFont.Tiny;
        GUI.color = pact != null ? skin.TextWarn : skin.TextDim;
        Rect sub = new(textX, name.yMax, right - textX, 18f);
        // Time first: an objective sentence is long and gets truncated, the time left must stay visible.
        string subline = pact != null
            ? pact.TicksLeft.ToStringTicksToPeriod() + " - " + pact.StatusLine
            : UmbralPacts.GroupLabel(umbral).CapitalizeFirst();
        Widgets.Label(sub, subline.Truncate(sub.width));
        GUI.color = Color.white;
        Text.Font = GameFont.Small;

        Rect status = new(row.xMax - RightColumn - Gap, row.y, RightColumn, RowHeight);
        Rect button = new(status.x, row.y + (RowHeight - ButtonHeight) / 2f, status.width, ButtonHeight);
        if (pact != null)
        {
            if (skin.Button(button, "BOR_DarkPact_Abandon".Translate(), tone: BorTone.Bad))
            {
                CompDarkPact.ConfirmAbandonUmbral(pact);
            }
            if (DebugSettings.ShowDevGizmos)
            {
                float half = (status.width - Gap) / 2f;
                float devY = row.y + RowHeight;
                if (skin.Button(new Rect(status.x, devY, half, ButtonHeight), "DEV: Complete"))
                {
                    pact.Succeed();
                }
                if (skin.Button(new Rect(status.x + half + Gap, devY, half, ButtonHeight), "DEV: Expire"))
                {
                    pact.Fail("BOR_DarkPact_Reason_Expired".Translate());
                }
            }
        }
        else if (done)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = skin.TextDim;
            Widgets.Label(status, "BOR_UmbralPact_Complete".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }
        else
        {
            string? blocked = mirror.UmbralBlockReason(umbral);
            if (blocked != null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
            }
            if (skin.Button(button, "BOR_UmbralPact_MakePact".Translate(), active: blocked == null, tone: BorTone.Bad) && blocked == null)
            {
                mirror.OfferUmbral(umbral);
            }
            GUI.color = Color.white;
            if (blocked != null)
            {
                TooltipHandler.TipRegion(button, blocked);
            }
        }

        Rect tipArea = new(row.x, row.y, row.width - RightColumn - Gap, row.height);
        TooltipHandler.TipRegion(tipArea, () => pact != null
            ? "BOR_UmbralPact_ActiveTip".Translate(umbral.LabelShortCap, pact.objectiveText,
                "BOR_DarkPact_TimeLeft".Translate(pact.TicksLeft.ToStringTicksToPeriod())).Resolve()
            : done
                ? "BOR_UmbralPact_Done".Translate().Resolve()
                : "BOR_UmbralPact_RowTip".Translate(umbral.LabelShortCap, UmbralPacts.GroupLabel(umbral), QuestNode_Root_UmbralPact.DurationLabel).Resolve(),
            7395000 + index);
    }

    private static string MissingList(ResearchProjectDef r) => (r.prerequisites ?? new List<ResearchProjectDef>())
        .Concat(r.hiddenPrerequisites ?? new List<ResearchProjectDef>()).Where(p => !p.IsFinished).Select(p => p.LabelCap.Resolve()).ToCommaList();

    private static void DrawRow(Rect row, ResearchProjectDef research, string? blocked, CompDarkPact mirror, int index)
    {
        BorSkin skin = BorSkin.Current;
        bool granted = research.IsFinished;
        skin.Row(row, false, hoverable: true, alternate: index % 2 == 1);

        ThingDef? iconDef = DarkPacts.IconDefFor(research);
        Rect icon = new(row.x + Gap, row.y + (row.height - IconSize) / 2f, IconSize, IconSize);
        if (granted || blocked != null)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
        }
        if (iconDef != null)
        {
            Widgets.DefIcon(icon, iconDef);
        }
        else
        {
            GUI.DrawTexture(icon, mirror.parent.def.uiIcon);
        }
        GUI.color = Color.white;

        float textX = icon.xMax + Gap * 2f;
        float right = row.xMax - RightColumn - Gap;
        Rect name = new(textX, row.y + 6f, right - textX, 22f);
        Text.Font = GameFont.Small;
        Widgets.Label(name, (iconDef?.LabelCap ?? research.LabelCap).Truncate(name.width));
        if (iconDef != null)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = skin.TextDim;
            Rect sub = new(textX, name.yMax, right - textX, 18f);
            Widgets.Label(sub, research.LabelCap.Truncate(sub.width));
            GUI.color = Color.white;
        }
        Text.Font = GameFont.Small;

        Rect status = new(row.xMax - RightColumn - Gap, row.y, RightColumn, row.height);
        if (granted)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = skin.TextDim;
            Widgets.Label(status, "BOR_DarkPact_Granted".Translate());
        }
        else if (DarkPacts.Active?.research == research)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = skin.TextWarn;
            Widgets.Label(status, "BOR_DarkPact_UnderWay".Translate());
        }
        else if (research.PrerequisitesCompleted)
        {
            // Ready, or held back only by the moment (a pact under way, no power): the button stays, greyed out, with the reason.
            Rect button = new(status.x, row.y + (row.height - ButtonHeight) / 2f, status.width, ButtonHeight);
            if (blocked != null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
            }
            if (skin.Button(button, "BOR_DarkPact_MakePact".Translate(), active: blocked == null, tone: BorTone.Bad) && blocked == null)
            {
                mirror.Offer(research);
            }
            GUI.color = Color.white;
            if (blocked != null)
            {
                TooltipHandler.TipRegion(button, blocked);
            }
        }
        else
        {
            // Clipped to the row; the full list is in the tooltip.
            Text.Font = GameFont.Tiny;
            GUI.color = skin.TextWarn;
            GUI.BeginGroup(status.ContractedBy(0f, 4f));
            Widgets.Label(new Rect(0f, 0f, status.width, row.height - 8f), "BOR_DarkPact_Needs".Translate(MissingList(research)));
            GUI.EndGroup();
            TooltipHandler.TipRegion(status, blocked);
        }
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        TooltipHandler.TipRegion(row, () =>
        {
            string unlocks = research.UnlockedDefs.Select(d => d.LabelCap.Resolve()).Distinct().ToCommaList();
            return "BOR_DarkPact_RowTip".Translate(research.LabelCap, research.description, unlocks.NullOrEmpty() ? "-" : unlocks,
                QuestNode_Root_DarkPact.DurationLabel).Resolve();
        }, 7394000 + index);
    }
}
