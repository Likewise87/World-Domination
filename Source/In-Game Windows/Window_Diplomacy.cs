using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using RimWorld.Planet;
using TSA_WorldDomination;
using Verse.Sound;

namespace WorldDomination
{
    public class Window_DiplomacyMatrix : Window
    {
        private Vector2 scrollPos;
        public override Vector2 InitialSize => new Vector2(UI.screenWidth, UI.screenHeight);

        private string sortColumn = "Name";
        private bool sortAscending = true;
        private string searchTerm = "";

        private List<Faction> cachedFactions;
        private int lastUpdateTick = -9999;

        private const float MinRowHeight = 60f;
        private const float LineHeight = 22f;

        public Window_DiplomacyMatrix()
        {
            this.doCloseX = true;
            this.draggable = false;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var manager = Find.World.GetComponent<WorldComponent_SpreadManager>();
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 35f), "TSA_WD_DiplomacyMatrix_Title".Translate());

            // --- SEARCH BOX ---
            Rect searchRect = new Rect(0, 40f, 120f, 25f); // Slightly wider to accommodate names better
            Text.Font = GameFont.Tiny;
            string oldSearch = searchTerm;
            searchTerm = Widgets.TextField(searchRect, searchTerm);
            if (searchTerm != oldSearch) lastUpdateTick = -9999;

            if (string.IsNullOrEmpty(searchTerm))
            {
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(searchRect, "TSA_WD_FilterByName".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }

            // --- COLUMN SETUP ---
            float colFaction = 300f;
            float colThreat = 120f;
            float availableWidth = inRect.width - colFaction - colThreat - 60f;
            float colRel = availableWidth / 3f;

            // --- HEADERS ---
            Rect hRect = new Rect(0, 70f, inRect.width, 30f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;

            float curX = 0f;
            DrawHeader(ref curX, colFaction, "  " + "TSA_WD_Stats_H_Faction".Translate(), "Name", hRect);
            DrawHeader(ref curX, colRel, "  " + "TSA_WD_Diplomacy_HostileTo".Translate(), "Hostile", hRect);
            DrawHeader(ref curX, colRel, "  " + "TSA_WD_Diplomacy_NeutralWith".Translate(), "Neutral", hRect);
            DrawHeader(ref curX, colRel, "  " + "TSA_WD_Diplomacy_AllyOf".Translate(), "Ally", hRect);
            DrawHeader(ref curX, colThreat, "  " + "TSA_WD_Stats_H_Threat".Translate(), "Threat", hRect);

            GUI.color = Color.white;
            Widgets.DrawLineHorizontal(0, hRect.yMax, inRect.width);

            // --- DATA GATHERING ---
            if (Find.TickManager.TicksGame >= lastUpdateTick + 300 || cachedFactions == null)
            {
                var allVisible = Find.FactionManager.AllFactionsVisible.Where(f => !f.def.hidden && !f.defeated);

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    var filtered = allVisible.Where(f =>
                        f.Name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        f.def.LabelCap.ToString().IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0
                    );
                    cachedFactions = ApplySort(filtered, manager).ToList();
                }
                else
                {
                    var playerFaction = allVisible.Where(f => f.IsPlayer);
                    var others = allVisible.Where(f => !f.IsPlayer);
                    cachedFactions = playerFaction.Concat(ApplySort(others, manager)).ToList();
                }
                lastUpdateTick = Find.TickManager.TicksGame;
            }

            // --- SCROLL VIEW ---
            Dictionary<Faction, float> rowHeights = new Dictionary<Faction, float>();
            float totalContentHeight = 0f;
            foreach (var f in cachedFactions)
            {
                int maxCount = Math.Max(1, Math.Max(GetRelationCount(f, FactionRelationKind.Hostile),
                                         Math.Max(GetRelationCount(f, FactionRelationKind.Neutral),
                                                  GetRelationCount(f, FactionRelationKind.Ally))));
                float h = Math.Max(MinRowHeight, maxCount * LineHeight + 15f);
                rowHeights[f] = h;
                totalContentHeight += h;
            }

            Rect viewRect = new Rect(0, 0, inRect.width - 25f, totalContentHeight);
            Widgets.BeginScrollView(new Rect(0, 110f, inRect.width, inRect.height - 130f), ref scrollPos, viewRect);

            float currentY = 0f;
            for (int i = 0; i < cachedFactions.Count; i++)
            {
                Faction f = cachedFactions[i];
                float h = rowHeights[f];
                Rect row = new Rect(0, currentY, viewRect.width, h);

                if (f == Faction.OfPlayer)
                {
                    GUI.color = new Color(0.2f, 0.5f, 0.8f, 0.15f);
                    Widgets.DrawBoxSolid(row, GUI.color);
                    GUI.color = Color.white;
                }
                else if (i % 2 == 0) Widgets.DrawHighlight(row);
                Widgets.DrawLineHorizontal(0, currentY + h, viewRect.width);

                // 1. Identity (Interactive Name)
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect iconRect = new Rect(row.x + 20, currentY + (h / 2) - 20, 40, 40);
                WorldDomination_UIUtils.DrawFactionIconWithColor(iconRect, f);

                Rect nameRect = new Rect(iconRect.xMax + 15, currentY + (h / 2) - 22, colFaction - 90, 25);
                Text.Font = GameFont.Small;

                // Clicking the main faction name sets it as the filter
                if (Widgets.ButtonText(nameRect, f.Name.Colorize(f == Faction.OfPlayer ? Color.cyan : Color.white), false, true, true))
                {
                    searchTerm = f.Name;
                    lastUpdateTick = -9999;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }

                Rect typeRect = new Rect(nameRect.x, nameRect.yMax - 2, nameRect.width, 20);
                Text.Font = GameFont.Tiny;
                GUI.color = Color.gray;
                Widgets.Label(typeRect, f.def.LabelCap);
                GUI.color = Color.white;

                // 2. Relations (Interactive Vertical Lists)
                float rX = colFaction;
                DrawRelationList(new Rect(rX, currentY + 5f, colRel, h - 10f), f, FactionRelationKind.Hostile);
                rX += colRel;
                DrawRelationList(new Rect(rX, currentY + 5f, colRel, h - 10f), f, FactionRelationKind.Neutral);
                rX += colRel;
                DrawRelationList(new Rect(rX, currentY + 5f, colRel, h - 10f), f, FactionRelationKind.Ally);
                rX += colRel;

                // 3. Threat Multiplier
                if (f != null && !f.IsPlayer)
                {
                    float mult = manager?.GetThreatScoreFor(f) ?? 1f;
                    Rect threatRect = new Rect(rX + 10f, currentY, colThreat - 10f, h);

                    Text.Anchor = TextAnchor.MiddleLeft;
                    Text.Font = GameFont.Small;
                    GUI.color = (mult <= 0.41f) ? Color.gray : Color.Lerp(Color.white, Color.red, (mult - 1f) / 3f);

                    Widgets.Label(threatRect, $"x{mult:F2}");

                    if (Mouse.IsOver(threatRect))
                        TooltipHandler.TipRegion(threatRect, manager.GetThreatBreakdown(f));

                    GUI.color = Color.white;
                }

                currentY += h;
            }

            Widgets.EndScrollView();
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawRelationList(Rect rect, Faction subject, FactionRelationKind kind)
        {
            var others = Find.FactionManager.AllFactionsVisible
                .Where(other => other != subject && !other.def.hidden && subject.RelationKindWith(other) == kind)
                .ToList();

            if (!others.Any())
            {
                GUI.color = new Color(1f, 1f, 1f, 0.3f);
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect leftAlignedRect = new Rect(rect.x + 10f, rect.y, rect.width - 10f, rect.height);
                Widgets.Label(leftAlignedRect, "---");
                GUI.color = Color.white;
                return;
            }

            for (int i = 0; i < others.Count; i++)
            {
                Rect lineRect = new Rect(rect.x + 10, rect.y + (i * LineHeight), rect.width - 10, LineHeight);
                Faction other = others[i];

                // Make the relationship entry a button
                if (Widgets.ButtonInvisible(lineRect))
                {
                    searchTerm = other.Name;
                    lastUpdateTick = -9999;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }

                if (Mouse.IsOver(lineRect)) Widgets.DrawHighlight(lineRect);

                GUI.color = other.Color;
                GUI.DrawTexture(new Rect(lineRect.x, lineRect.y + 3, 16, 16), other.def.FactionIcon);
                GUI.color = Color.white;

                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(lineRect.x + 22, lineRect.y, lineRect.width - 22, LineHeight), other.Name.Colorize(other.Color));
            }
        }

        private IEnumerable<Faction> ApplySort(IEnumerable<Faction> list, WorldComponent_SpreadManager manager)
        {
            Func<Faction, object> keySelector = sortColumn switch
            {
                "Name" => f => f.Name,
                "Hostile" => f => GetRelationCount(f, FactionRelationKind.Hostile),
                "Neutral" => f => GetRelationCount(f, FactionRelationKind.Neutral),
                "Ally" => f => GetRelationCount(f, FactionRelationKind.Ally),
                "Threat" => f => manager?.GetThreatScoreFor(f) ?? 0f,
                _ => f => f.Name
            };
            return sortAscending ? list.OrderBy(keySelector) : list.OrderByDescending(keySelector);
        }

        private int GetRelationCount(Faction f, FactionRelationKind kind)
        {
            return Find.FactionManager.AllFactionsVisible.Count(other => other != f && !other.def.hidden && f.RelationKindWith(other) == kind);
        }

        private void DrawHeader(ref float curX, float width, string label, string tag, Rect hRect)
        {
            Rect headerRect = new Rect(curX, hRect.y, width, hRect.height);
            if (Mouse.IsOver(headerRect)) Widgets.DrawHighlight(headerRect);

            string headerText = label + (sortColumn == tag ? (sortAscending ? " ▲" : " ▼") : "");
            Widgets.Label(headerRect, headerText);

            if (Widgets.ButtonInvisible(headerRect))
            {
                if (sortColumn == tag) sortAscending = !sortAscending;
                else { sortColumn = tag; sortAscending = true; }
                lastUpdateTick = -9999;
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            curX += width;
        }
    }
}