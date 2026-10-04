using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using RimWorld.Planet;
using Outposts;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions; // Added for robust string cleaning
using TSA_WorldDomination.WorldActions;
using TSA_WorldDomination;
using Verse.Sound;

namespace WorldDomination
{
    public class Window_OutpostOverview : Window
    {
        private Vector2 scrollPos;
        // Adjusted for Full Screen
        public override Vector2 InitialSize => new Vector2(UI.screenWidth, UI.screenHeight);

        // Sorting & Filtering state
        private string sortColumn = "Name";
        private bool sortAscending = true;
        private string searchTerm = "";

        // Performance Caching
        private int lastUpdateTick = -9999;
        private const int UpdateIntervalTicks = 300; // 300 ticks = 5 seconds
        private List<OutpostEntry> cachedList = new List<OutpostEntry>();

        private static readonly FieldInfo occupantsField = typeof(Outpost).GetField("occupants", BindingFlags.Instance | BindingFlags.NonPublic);

        // Simple helper class for caching
        private class OutpostEntry
        {
            public Outpost Outpost;
            public CompViralSpread Comp;
            public int Distance;
            public string ProdSummary;
            public int PawnCount;
            public int TicksLeft;
            public float FoodNet;
            public float FoodCurrent;
            public bool LogisticsActive;
        }

        public Window_OutpostOverview()
        {
            this.doCloseX = true;
            this.draggable = false; // Disabled for full screen
            this.preventCameraMotion = false;
            this.forcePause = false; // Pausing while in full screen overview is standard
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 35f), "TSA_WD_OutpostManager_Title".Translate());

            // --- REBALANCED COLUMN WIDTHS ---
            float colIcon = 80f;
            float colPadding = 20f;
            float colName = 160f;
            float colDist = 80f;
            float colPawns = 60f;
            float colFood = 135f;
            float colStrength = 100f;
            float colRoad = 135f;
            float colCooldown = 150f;
            float colWhat = 220f;
            float colWhen = 160f;
            float rowHeight = 55f;

            // --- SEARCH BOX ---
            Rect searchRect = new Rect(0, 40f, colIcon, 25f);
            Text.Font = GameFont.Tiny;
            string oldSearch = searchTerm;
            searchTerm = Widgets.TextField(searchRect, searchTerm);

            // Force refresh if search changes
            if (searchTerm != oldSearch) lastUpdateTick = -9999;

            if (string.IsNullOrEmpty(searchTerm))
            {
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(searchRect, "TSA_WD_FilterByType".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }

            // --- HEADERS ---
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Rect hRect = new Rect(0, 40f, inRect.width, 25f);
            float curX = colIcon + colPadding;

            DrawHeader(ref curX, colName, "TSA_WD_Outpost_Name".Translate(), "Name", hRect);
            DrawHeader(ref curX, colDist, "TSA_WD_Outpost_Dist".Translate(), "Dist", hRect);
            DrawHeader(ref curX, colPawns, "TSA_WD_Outpost_Pawns".Translate(), "Pawns", hRect);
            DrawHeader(ref curX, colFood, "TSA_WD_Outpost_FoodStatus".Translate(), "Food", hRect);
            DrawHeader(ref curX, colStrength, "TSA_WD_Outpost_Strength".Translate(), "Strength", hRect);
            DrawHeader(ref curX, colRoad, "TSA_WD_Outpost_Road".Translate(), "Road", hRect);
            DrawHeader(ref curX, colCooldown, "TSA_WD_Outpost_Status".Translate(), "Status", hRect);
            DrawHeader(ref curX, colWhat, "TSA_WD_Outpost_Produces".Translate(), "Produces", hRect);
            DrawHeader(ref curX, colWhen, "TSA_WD_Outpost_Output".Translate(), "Timer", hRect);

            GUI.color = Color.white;
            Widgets.DrawLineHorizontal(0, hRect.yMax, inRect.width);

            // --- DATA GATHERING (CACHED EVERY 5 SECONDS) ---
            if (Find.TickManager.TicksGame >= lastUpdateTick + UpdateIntervalTicks || cachedList == null)
            {
                var manager = Find.World.GetComponent<WorldComponent_SpreadManager>();
                var logi = Find.World.GetComponent<WorldComponent_LogisticsManager>();
                int playerTile = Find.WorldObjects.Settlements.FirstOrDefault(s => s.Faction == Faction.OfPlayer)?.Tile ?? -1;
                var ticksField = typeof(Outpost).GetField("ticksUntilProduction", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                var rawList = Find.WorldObjects.AllWorldObjects
                    .Where(o => o is Outpost && o.Faction == Faction.OfPlayer)
                    .Select(o => o as Outpost)
                    .Select(o => {
                        var lComp = o.GetComponent<CompOutpostLogistics>();
                        float net = logi.GetDailyProduction(o) - logi.GetDailyDemand(o)
                                    + logi.manualLinks.Where(l => l.destTile == o.Tile).Sum(l => l.manualAssignment * logi.GetEfficiency(l.sourceTile, l.destTile))
                                    - logi.manualLinks.Where(l => l.sourceTile == o.Tile).Sum(l => l.manualAssignment);
                        return new OutpostEntry
                        {
                            Outpost = o,
                            Comp = o.GetComponent<CompViralSpread>(),
                            Distance = (playerTile != -1) ? WorldActions_Utils.GetDistance(o.Tile, playerTile, manager) : 999,
                            ProdSummary = GetOutpostProductionSummary(o),
                            PawnCount = GetPawnCount(o),
                            TicksLeft = (int)(ticksField?.GetValue(o) ?? 0),
                            FoodNet = net,
                            FoodCurrent = lComp?.currentFood ?? 0f,
                            LogisticsActive = lComp?.produceForLogistics ?? false
                        };
                    });

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    rawList = rawList.Where(x => x.Outpost.def.defName.ToLower().Contains(searchTerm.ToLower()));
                }

                Func<OutpostEntry, object> keySelector = sortColumn switch
                {
                    "Name" => x => x.Outpost.LabelCap,
                    "Dist" => x => x.Distance,
                    "Pawns" => x => x.PawnCount,
                    "Food" => x => x.FoodNet,
                    "Strength" => x => x.Comp?.strength ?? 0f,
                    "Road" => x => x.Comp?.roadProgress ?? 0f,
                    "Status" => x => x.Comp?.IsOnCooldown ?? false,
                    "Produces" => x => x.ProdSummary,
                    "Timer" => x => x.TicksLeft,
                    _ => x => x.Outpost.LabelCap
                };

                cachedList = (sortAscending ? rawList.OrderBy(keySelector) : rawList.OrderByDescending(keySelector)).ToList();
                lastUpdateTick = Find.TickManager.TicksGame;
            }

            // --- SCROLL VIEW ---
            Rect viewRect = new Rect(0, 0, inRect.width - 25f, cachedList.Count * rowHeight);
            Widgets.BeginScrollView(new Rect(0, 70f, inRect.width, inRect.height - 100f), ref scrollPos, viewRect);

            for (int i = 0; i < cachedList.Count; i++)
            {
                var entry = cachedList[i];
                var outpost = entry.Outpost;
                var comp = entry.Comp;
                Rect row = new Rect(0, i * rowHeight, viewRect.width, rowHeight);

                if (i % 2 == 0) Widgets.DrawHighlight(row);
                if (Mouse.IsOver(row)) Widgets.DrawLightHighlight(row);

                // 1. Icon & Jump Logic
                Rect iconRect = new Rect(row.x + 10f, row.y + 7, 40, 40);
                if (outpost.def != null)
                {
                    GUI.color = outpost.Faction.Color;
                    GUI.DrawTexture(iconRect, outpost.def.ExpandingIconTexture);
                    GUI.color = Color.white;
                    if (Widgets.ButtonInvisible(iconRect))
                    {
                        CameraJumper.TryJump(outpost);
                        Find.WorldSelector.ClearSelection();
                        Find.WorldSelector.Select(outpost);
                        if (Find.MainTabsRoot.OpenTab == MainButtonDefOf.Factions) Find.MainTabsRoot.EscapeCurrentTab();
                        this.Close();
                    }
                    TooltipHandler.TipRegion(iconRect, "TSA_WD_JumpToOutpost".Translate(outpost.LabelCap));
                }

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                float rX = colIcon + colPadding;

                // 2. Name
                Widgets.Label(new Rect(rX, row.y, colName, rowHeight), outpost.LabelCap);
                rX += colName;

                // 3. Distance
                Widgets.Label(new Rect(rX, row.y, colDist, rowHeight), entry.Distance.ToString());
                rX += colDist;

                // 4. Pawns
                Widgets.Label(new Rect(rX, row.y, colPawns, rowHeight), entry.PawnCount.ToString());
                rX += colPawns;

                // 5. Food Status
                string netSign = entry.FoodNet >= 0 ? "+" : "";
                Color fCol = entry.FoodNet > 0.1f ? Color.green : (entry.FoodNet < -0.1f ? Color.red : Color.yellow);
                Widgets.Label(new Rect(rX, row.y, colFood, rowHeight), $"{netSign}{entry.FoodNet:F1}\n({entry.FoodCurrent:F0}/100)".Colorize(fCol));
                rX += colFood;

                // 6. Strength
                float curStr = comp?.strength ?? 0f;
                float maxStr = comp?.GetTargetOutpostStrength() ?? 0f;
                string strLabel = $"{curStr.ToString("F0")}/{maxStr.ToString("F0")}";
                Widgets.Label(new Rect(rX, row.y, colStrength, rowHeight), strLabel.Colorize(curStr >= maxStr ? Color.cyan : Color.white));
                rX += colStrength;

                // 7. Road Construction
                Rect roadRect = new Rect(rX, row.y, colRoad, rowHeight);
                if (comp != null && comp.roadTargetTile != -1)
                {
                    Rect barRect = new Rect(roadRect.x + 5, roadRect.y + 28, colRoad - 10, 14);
                    Text.Font = GameFont.Tiny;
                    // Correcting with SolidColorTexture for Texture2D conversion
                    Widgets.FillableBar(barRect, comp.roadProgress, SolidColorMaterials.NewSolidColorTexture(new Color(0.4f, 0.6f, 0.9f)));
                    Widgets.Label(barRect, $"{(comp.roadProgress * 100f).ToString("F0")}%");
                    Text.Font = GameFont.Small;
                }
                else
                {
                    GUI.color = Color.gray;
                    Widgets.Label(roadRect, "TSA_WD_None".Translate());
                    GUI.color = Color.white;
                }
                rX += colRoad;

                // 8. Cooldown Status
                Rect statusRect = new Rect(rX, row.y, colCooldown, rowHeight);
                GUI.color = (comp != null && comp.IsOnCooldown) ? Color.yellow : new Color(0.8f, 0.8f, 0.2f);
                Widgets.Label(statusRect, (comp != null && comp.IsOnCooldown) ? "TSA_WD_Cooldown".Translate() : "TSA_WD_Ready".Translate());
                GUI.color = Color.white;
                rX += colCooldown;


                // 9. Produces
                Text.Font = GameFont.Tiny;
                string prodLabel = entry.LogisticsActive
                    ? "TSA_LogisticsActive".Translate().Colorize(Color.cyan)
                    : entry.ProdSummary;
                Widgets.Label(new Rect(rX, row.y, colWhat, rowHeight), prodLabel);
                rX += colWhat;

                // 10. Output Timer
                Text.Font = GameFont.Small;
                string timeLabel = entry.LogisticsActive
                    ? "TSA_LogisticsProductionFrozen".Translate().Colorize(Color.yellow)
                    : outpost.TimeTillProduction;
                Widgets.Label(new Rect(rX, row.y, colWhen, rowHeight), timeLabel);
            }
            Widgets.EndScrollView();
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawHeader(ref float curX, float width, string label, string tag, Rect hRect)
        {
            Rect headerRect = new Rect(curX, hRect.y, width, hRect.height);
            if (Mouse.IsOver(headerRect)) Widgets.DrawHighlight(headerRect);

            Text.Anchor = TextAnchor.LowerCenter;
            string headerText = label + (sortColumn == tag ? (sortAscending ? " ▲" : " ▼") : "");
            Widgets.Label(headerRect, headerText);

            if (Widgets.ButtonInvisible(headerRect)) SetSort(tag);
            curX += width;
        }

        private void SetSort(string col)
        {
            if (sortColumn == col) sortAscending = !sortAscending;
            else { sortColumn = col; sortAscending = true; }
            lastUpdateTick = -9999; // Force refresh data to apply new sort
            SoundDefOf.Click.PlayOneShotOnCamera();
        }

        private int GetPawnCount(Outpost outpost)
        {
            if (occupantsField?.GetValue(outpost) is List<Pawn> occupants) return occupants.Count;
            return 0;
        }

        private string GetOutpostProductionSummary(Outpost outpost)
        {
            // First check ResultOptions
            if (outpost.ResultOptions != null && outpost.ResultOptions.Count > 0)
            {
                var pawns = outpost.CapablePawns.ToList();
                var parts = outpost.ResultOptions.Select(ro => $"{ro.Amount(pawns)}x {ro.Thing.label}");
                return string.Join(", ", parts);
            }

            // Fallback for custom logic like Towns
            string summary = "";
            MethodInfo method = outpost.GetType().GetMethod("ProductionString", BindingFlags.Public | BindingFlags.Instance);
            if (method != null) summary = (string)method.Invoke(outpost, null);
            else
            {
                PropertyInfo prop = outpost.GetType().GetProperty("ProductionString", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null) summary = (string)prop.GetValue(outpost);
            }

            if (string.IsNullOrEmpty(summary)) return "TSA_WD_Produces_Nothing".Translate();

            string firstLine = summary.Split('\n')[0];

            summary = Regex.Replace(firstLine, @"\s+in\s+.*", "", RegexOptions.IgnoreCase).Trim();

            int idx = summary.IndexOfAny(new char[] { '(', '\n' });
            return idx != -1 ? summary.Substring(0, idx).Trim() : summary.Trim();
        }

        private int GetRoadTotalSteps(int start, int dest)
        {
            if (dest == -1) return 0;
            var layer = Find.WorldGrid.PlanetLayers.FirstOrDefault().Value;
            using (WorldPathing pathing = new WorldPathing(layer))
            {
                using (WorldPath path = pathing.FindPath(new PlanetTile(start, layer), new PlanetTile(dest, layer), null))
                    return (path != null && path.Found) ? path.NodesReversed.Count - 1 : 0;
            }
        }

        private int GetRoadRemainingSteps(int start, int dest)
        {
            if (dest == -1) return 0;
            var layer = Find.WorldGrid.PlanetLayers.FirstOrDefault().Value;
            using (WorldPathing pathing = new WorldPathing(layer))
            {
                using (WorldPath path = pathing.FindPath(new PlanetTile(start, layer), new PlanetTile(dest, layer), null))
                {
                    if (path == null || !path.Found) return 0;
                    var nodes = path.NodesReversed;
                    int unpaved = 0;
                    for (int i = nodes.Count - 1; i > 0; i--) { if (!HasRoadLink(nodes[i], nodes[i - 1])) unpaved++; }
                    return unpaved;
                }
            }
        }

        private bool HasRoadLink(int tileA, int tileB)
        {
            var tile = Find.WorldGrid[tileA] as SurfaceTile;
            return tile?.potentialRoads?.Any(r => r.neighbor.tileId == tileB) ?? false;
        }
    }
}