using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using RimWorld.Planet;
using Outposts;
using static WorldDomination.SpreadLogEntry;
using Verse.Sound;

namespace WorldDomination
{
    public class Window_WorldStats : Window
    {
        private Vector2 scrollPos;
        public override Vector2 InitialSize => new Vector2(UI.screenWidth, UI.screenHeight);

        private GlobalWorldStats statsData;
        private List<FactionStat> displayList;

        private int sortColumn = 6;
        private bool sortDescending = true;

        public Window_WorldStats()
        {
            this.doCloseX = true;
            this.draggable = false;
            this.absorbInputAroundWindow = true;
            this.forcePause = true;

            this.statsData = WorldActions_Utils.GetWorldPowerStats();
            displayList = new List<FactionStat>(statsData.FactionStats);

            Faction pFact = Find.FactionManager.OfPlayer;
            if (pFact != null && !displayList.Any(fs => fs.faction == pFact))
            {
                var playerStats = GetPlayerOutpostStats();
                displayList.Add(playerStats);
                for (int t = 1; t <= 4; t++) statsData.GlobalTierStr[t] += playerStats.strength[t];
                statsData.GlobalTotalStr += playerStats.TotalStr;
            }

            SortList();
        }

        private void SortList()
        {
            switch (sortColumn)
            {
                case 0:
                    displayList = sortDescending ? displayList.OrderByDescending(x => x.faction.Name).ToList() : displayList.OrderBy(x => x.faction.Name).ToList();
                    break;
                case 2:
                case 3:
                case 4:
                case 5:
                    int tierIdx = sortColumn - 1;
                    displayList = sortDescending ? displayList.OrderByDescending(x => x.strength[tierIdx]).ToList() : displayList.OrderBy(x => x.strength[tierIdx]).ToList();
                    break;
                case 6:
                    displayList = sortDescending ? displayList.OrderByDescending(x => x.TotalStr).ToList() : displayList.OrderBy(x => x.TotalStr).ToList();
                    break;
            }
        }

        private void HeaderButton(Rect rect, string label, int columnId, bool sortable = true, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            if (sortable && Widgets.ButtonInvisible(rect))
            {
                if (sortColumn == columnId) sortDescending = !sortDescending;
                else { sortColumn = columnId; sortDescending = true; }
                SoundDefOf.Click.PlayOneShotOnCamera();
                SortList();
            }

            Text.Anchor = anchor;
            GUI.color = (sortColumn == columnId) ? Color.cyan : Color.gray;
            string indicator = (sortable && sortColumn == columnId) ? (sortDescending ? " ▼" : " ▲") : "";

            // Adjust label rect based on alignment to keep padding consistent
            Rect labelRect = (anchor == TextAnchor.MiddleCenter) ? rect : new Rect(rect.x + 5, rect.y, rect.width - 5, rect.height);
            Widgets.Label(labelRect, label + indicator);
            GUI.color = Color.white;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var manager = Find.World.GetComponent<WorldComponent_SpreadManager>();
            int ticksNow = Find.TickManager.TicksGame;

            // --- MANUAL LAYOUT CONFIGURATION ---
            float colFaction = 340f;
            float colStatus = 150f;
            float colT = 170f;
            float colAction = 80f;
            float rowHeight = 75f;
            float footerHeight = 65f;

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(new Rect(0, 0, inRect.width, 35f), "TSA_WD_Stats_Title".Translate());

            // --- HEADERS ---
            Rect hRect = new Rect(0, 50f, inRect.width, 30f);
            Text.Font = GameFont.Small;

            HeaderButton(new Rect(0, hRect.y, colFaction, 30f), "TSA_WD_Stats_H_Faction".Translate(), 0, true, TextAnchor.MiddleLeft);
            HeaderButton(new Rect(colFaction, hRect.y, colStatus, 30f), "TSA_WD_Stats_H_Status".Translate(), 1, false, TextAnchor.MiddleLeft);

            float curX = colFaction + colStatus;
            // CENTERED DATA HEADERS
            HeaderButton(new Rect(curX, hRect.y, colT, 30f), "TSA_WD_Stats_H_T1".Translate(), 2, true, TextAnchor.MiddleCenter);
            HeaderButton(new Rect(curX + colT, hRect.y, colT, 30f), "TSA_WD_Stats_H_T2".Translate(), 3, true, TextAnchor.MiddleCenter);
            HeaderButton(new Rect(curX + (colT * 2), hRect.y, colT, 30f), "TSA_WD_Stats_H_T3".Translate(), 4, true, TextAnchor.MiddleCenter);
            HeaderButton(new Rect(curX + (colT * 3), hRect.y, colT, 30f), "TSA_WD_Stats_H_T4".Translate(), 5, true, TextAnchor.MiddleCenter);
            HeaderButton(new Rect(curX + (colT * 4), hRect.y, colT, 30f), "TSA_WD_Stats_H_Influence".Translate(), 6, true, TextAnchor.MiddleCenter);

            // --- SCROLL VIEW ---
            Rect viewRect = new Rect(0, 0, inRect.width - 25f, displayList.Count * rowHeight);
            Rect scrollRect = new Rect(0, 90f, inRect.width, inRect.height - 90f - footerHeight);
            Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect);

            for (int i = 0; i < displayList.Count; i++)
            {
                var fs = displayList[i];
                Rect row = new Rect(0, i * rowHeight, viewRect.width, rowHeight);

                if (fs.faction == Faction.OfPlayer) Widgets.DrawBoxSolid(row, new Color(0.2f, 0.5f, 0.8f, 0.15f));
                else if (i % 2 == 0) Widgets.DrawHighlight(row);

                // 1. IDENTITY (Left Aligned)
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect iconRect = new Rect(row.x + 10, row.y + (rowHeight / 2 - 20), 40, 40);
                WorldDomination_UIUtils.DrawFactionIconWithColor(iconRect, fs.faction);

                Rect nameArea = new Rect(iconRect.xMax + 10, row.y + 15, colFaction - 65, 25);
                Widgets.Label(nameArea, fs.faction.Name.Truncate(nameArea.width).Colorize(fs.faction == Faction.OfPlayer ? Color.cyan : Color.white));

                string tooltipText = fs.faction.IsPlayer ? (string)fs.faction.def.LabelCap : fs.faction.def.LabelCap + "\n\n" + fs.faction.GetInfoText();
                TooltipHandler.TipRegion(nameArea, tooltipText);

                Rect typeArea = new Rect(nameArea.x, nameArea.yMax, nameArea.width, 20);
                Text.Font = GameFont.Tiny;
                GUI.color = Color.gray;
                Widgets.Label(typeArea, fs.faction.def.LabelCap.Truncate(typeArea.width));
                GUI.color = Color.white;

                // 2. STATUS (Left Aligned)
                float sX = colFaction + 5;
                float sV = rowHeight / 3f;
                Text.Anchor = TextAnchor.MiddleLeft;

                if (fs.faction == manager.currentWorldLeader && ticksNow < manager.leaderHandicapExpiryTick)
                {
                    Rect r = new Rect(sX, row.y, colStatus - 5, sV);
                    Widgets.Label(r, "TSA_WD_Status_Debuff".Translate().Colorize(Color.red));
                    TooltipHandler.TipRegion(r, "TSA_WD_Status_DebuffTip".Translate(((manager.leaderHandicapExpiryTick - ticksNow) / 60000f).ToString("F1")));
                }
                if (fs.faction == manager.currentWeakestUnderdog && ticksNow < manager.underdogBuffExpiryTick)
                {
                    Rect r = new Rect(sX, row.y + sV, colStatus - 5, sV);
                    Widgets.Label(r, "TSA_WD_Status_Buff".Translate().Colorize(Color.green));
                    TooltipHandler.TipRegion(r, "TSA_WD_Status_BuffTip".Translate(((manager.underdogBuffExpiryTick - ticksNow) / 60000f).ToString("F1")));
                }
                if (fs.faction == manager.expansionistZealFaction && ticksNow < manager.expansionistZealExpiryTick)
                {
                    Rect r = new Rect(sX, row.y + (sV * 2), colStatus - 5, sV);
                    Widgets.Label(r, "TSA_WD_Status_Zeal".Translate().Colorize(Color.cyan));
                    TooltipHandler.TipRegion(r, "TSA_WD_Status_ZealTip".Translate(((manager.expansionistZealExpiryTick - ticksNow) / 60000f).ToString("F1")));
                }

                // 3. TIER DATA (Middle Centered)
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                for (int t = 1; t <= 4; t++)
                {
                    float dX = colFaction + colStatus + (colT * (t - 1));
                    Rect tRect = new Rect(dX, row.y, colT, rowHeight);
                    float share = (statsData.GlobalTierStr[t] > 0 ? (fs.strength[t] / statsData.GlobalTierStr[t]) : 0);

                    Widgets.Label(new Rect(tRect.x, tRect.y + 5, tRect.width, 22f), fs.counts[t].ToString());
                    Widgets.Label(new Rect(tRect.x, tRect.y + 27, tRect.width, 22f), fs.strength[t].ToString("F0"));
                    Widgets.Label(new Rect(tRect.x, tRect.y + 49, tRect.width, 22f), share.ToString("P0"));

                    TooltipHandler.TipRegion(tRect, $"Tier {t} Statistics\n\nCount: {fs.counts[t]}\nTotal T{t} Strength: {fs.strength[t]:F0}\nShare of global T{t} Strength: {share:P1}");
                }

                // 4. TOTALS (Middle Centered)
                float totX = colFaction + colStatus + (colT * 4);
                Rect totRect = new Rect(totX, row.y, colT, rowHeight);
                float totalShare = statsData.GlobalTotalStr > 0 ? (fs.TotalStr / statsData.GlobalTotalStr) : 0;

                Widgets.Label(new Rect(totRect.x, totRect.y + 5, totRect.width, 22f), fs.TotalCount.ToString().Colorize(Color.cyan));
                Widgets.Label(new Rect(totRect.x, totRect.y + 27, totRect.width, 22f), fs.TotalStr.ToString("F0").Colorize(Color.cyan));
                Widgets.Label(new Rect(totRect.x, totRect.y + 49, totRect.width, 22f), totalShare.ToString("P1").Colorize(Color.cyan));

                TooltipHandler.TipRegion(totRect, $"Faction Total Stats\n\nTotal Tier Settlements: {fs.TotalCount}\nTotal Tier Strength: {fs.TotalStr:F0}\nShare of global Strength: {totalShare:P1}");

                // 5. DETAILS
                if (Widgets.ButtonText(new Rect(row.xMax - 105f, row.y + (rowHeight / 2 - 13), 100f, 26f), "TSA_WD_Log_BtnDetails".Translate()))
                    Find.WindowStack.Add(new Window_FactionDetails(fs.faction));
            }
            Widgets.EndScrollView();

            // --- 6. FOOTER (Middle Centered for Data) ---
            Rect footerRect = new Rect(0, inRect.height - footerHeight, inRect.width, footerHeight);
            Widgets.DrawBoxSolid(footerRect, new Color(1f, 1f, 1f, 0.05f));
            GUI.color = Color.cyan;
            Widgets.DrawLineHorizontal(footerRect.x, footerRect.y, footerRect.width);
            GUI.color = Color.white;

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(15, footerRect.y, colFaction + colStatus, footerHeight), "TSA_WD_Stats_WorldTotals".Translate().Colorize(Color.cyan));

            Text.Anchor = TextAnchor.MiddleCenter;
            float footX = colFaction + colStatus;
            for (int t = 1; t <= 4; t++)
            {
                Rect fTRect = new Rect(footX + (colT * (t - 1)), footerRect.y, colT, footerHeight);
                Widgets.Label(new Rect(fTRect.x, fTRect.y + 10, fTRect.width, 22f), displayList.Sum(x => x.counts[t]).ToString());
                Widgets.Label(new Rect(fTRect.x, fTRect.y + 32, fTRect.width, 22f), statsData.GlobalTierStr[t].ToString("F0"));
            }

            Rect fTotRect = new Rect(footX + (colT * 4), footerRect.y, colT, footerHeight);
            Widgets.Label(new Rect(fTotRect.x, fTotRect.y + 10, fTotRect.width, 22f), displayList.Sum(x => x.TotalCount).ToString());
            Widgets.Label(new Rect(fTotRect.x, fTotRect.y + 32, fTotRect.width, 22f), statsData.GlobalTotalStr.ToString("F0"));

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private FactionStat GetPlayerOutpostStats()
        {
            var stats = new FactionStat { faction = Faction.OfPlayer };
            if (stats.faction == null) return stats;
            var playerOutposts = Find.WorldObjects.AllWorldObjects.OfType<Outpost>().Where(o => o.Faction == Faction.OfPlayer);
            foreach (var outpost in playerOutposts)
            {
                var comp = outpost.GetComponent<CompViralSpread>();
                float str = comp?.strength ?? 0f;
                int pawnCount = 0;
                var field = typeof(Outpost).GetField("occupants", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field?.GetValue(outpost) is System.Collections.IList occupants) pawnCount = occupants.Count;
                int tier = pawnCount > 10 ? 4 : pawnCount > 7 ? 3 : pawnCount > 3 ? 2 : 1;
                stats.counts[tier]++;
                stats.strength[tier] += str;
            }
            return stats;
        }
    }
}