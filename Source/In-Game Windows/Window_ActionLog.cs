using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;
using RimWorld.Planet;

namespace WorldDomination
{
    public class Window_ActionLog : Window
    {
        private Vector2 scrollPos;
        // Fullscreen size
        public override Vector2 InitialSize => new Vector2(UI.screenWidth, UI.screenHeight);

        // --- FILTERS ---
        private string filterActor = "";
        private string filterTarget = "";
        private string filterMessage = "";

        public Window_ActionLog()
        {
            this.doCloseX = true;
            this.draggable = false; // Fullscreen usually fixed
            this.forcePause = false; // Do not pause game
            this.absorbInputAroundWindow = false; // Click-through behavior
            this.preventCameraMotion = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 35f), "TSA_WD_Log_WindowTitle".Translate());

            // --- COLUMN LAYOUT DEFINITION ---
            float colTime = 120f;
            float col12Width = 350f;
            float col3Width = inRect.width - colTime - (col12Width * 2) - 120f;
            float btnWidth = 90f;

            // --- SEARCH/FILTER BOXES ---
            float filterY = 40f;
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;

            // Actor Filter
            filterActor = Widgets.TextField(new Rect(colTime + 30f, filterY, col12Width - 30f, 20f), filterActor);
            if (filterActor.NullOrEmpty()) Widgets.Label(new Rect(colTime + 35f, filterY, col12Width - 35f, 20f), "TSA_WD_FilterByActor".Translate());

            // Target Filter
            filterTarget = Widgets.TextField(new Rect(colTime + col12Width + 40f, filterY, col12Width - 30f, 20f), filterTarget);
            if (filterTarget.NullOrEmpty()) Widgets.Label(new Rect(colTime + col12Width + 45f, filterY, col12Width - 35f, 20f), "TSA_WD_FilterByTarget".Translate());

            // Message Filter
            filterMessage = Widgets.TextField(new Rect(colTime + (col12Width * 2) + 20f, filterY, col3Width, 20f), filterMessage);
            if (filterMessage.NullOrEmpty()) Widgets.Label(new Rect(colTime + (col12Width * 2) + 25f, filterY, col3Width - 5f, 20f), "TSA_WD_FilterByMessage".Translate());

            // --- HEADER SETUP ---
            // Adjusted headerRect.y to match the baseline of filters better
            Rect headerRect = new Rect(0, 65f, inRect.width, 25f);
            Text.Font = GameFont.Tiny; GUI.color = Color.gray;
            Text.Anchor = TextAnchor.MiddleLeft;

            Widgets.Label(new Rect(0, headerRect.y, colTime, 25f), "  " + "TSA_WD_Log_HeaderTime".Translate());
            Widgets.Label(new Rect(colTime + 30f, headerRect.y, col12Width - 30f, 25f), "TSA_WD_Log_HeaderActor".Translate());
            Widgets.Label(new Rect(colTime + col12Width + 40f, headerRect.y, col12Width - 30f, 25f), "TSA_WD_Log_HeaderTarget".Translate());
            Widgets.Label(new Rect(colTime + (col12Width * 2) + 20f, headerRect.y, col3Width, 25f), "TSA_WD_Log_HeaderAction".Translate());

            GUI.color = Color.white; Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.DrawLineHorizontal(0, headerRect.yMax, inRect.width);

            var manager = Find.World.GetComponent<WorldComponent_SpreadManager>();
            if (manager == null) return;

            // --- DATA GATHERING & FILTERING ---
            var allLogs = manager.GetLog().AsEnumerable().Reverse(); // Newest first

            if (!filterActor.NullOrEmpty())
                allLogs = allLogs.Where(l => l.labelA.IndexOf(filterActor, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!filterTarget.NullOrEmpty())
                allLogs = allLogs.Where(l => l.labelB.IndexOf(filterTarget, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!filterMessage.NullOrEmpty())
                allLogs = allLogs.Where(l => l.message.IndexOf(filterMessage, StringComparison.OrdinalIgnoreCase) >= 0);

            var logs = allLogs.ToList();

            // --- SCROLL VIEW ---
            Rect viewRect = new Rect(0, 0, inRect.width - 25f, logs.Count * 38f);
            Widgets.BeginScrollView(new Rect(0, 95f, inRect.width, inRect.height - 110f), ref scrollPos, viewRect);

            for (int i = 0; i < logs.Count; i++)
            {
                var entry = logs[i];
                Rect row = new Rect(0, i * 38f, viewRect.width, 36f);
                if (i % 2 == 0) Widgets.DrawHighlight(row);

                // --- Column 0: Time (Surgically Vertically Centered) ---
                Rect timeRect = new Rect(row.x + 5f, row.y, colTime, row.height);
                GUI.color = Color.gray; Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(timeRect, FormatTicks(entry.timestamp));
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white; Text.Font = GameFont.Small;

                // --- Column 1: Actor ---
                Rect iconARect = new Rect(row.x + colTime, row.y + 6f, 24f, 24f);
                WorldDomination_UIUtils.DrawFactionIconWithColor(iconARect, entry.targetA);
                DrawJumpLabel(new Rect(row.x + colTime + 30f, row.y, col12Width - 30f, 36f), entry.labelA, entry.targetA);

                // --- Column 2: Target ---
                Rect iconBRect = new Rect(row.x + colTime + col12Width + 10f, row.y + 6f, 24f, 24f);
                WorldDomination_UIUtils.DrawFactionIconWithColor(iconBRect, entry.targetB);
                DrawJumpLabel(new Rect(row.x + colTime + col12Width + 40f, row.y, col12Width - 30f, 36f), entry.labelB, entry.targetB);

                // --- Column 3: Action Message ---
                Rect colCRect = new Rect(row.x + colTime + (col12Width * 2) + 20f, row.y, col3Width, 36f);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(colCRect, entry.message);
                Text.Anchor = TextAnchor.UpperLeft;

                // --- Column 4: Raid Details Button ---
                if (entry.isRaid)
                {
                    Rect btnRect = new Rect(row.xMax - btnWidth - 10f, row.y + 6f, btnWidth, 24f);
                    if (Widgets.ButtonText(btnRect, "TSA_WD_Log_BtnDetails".Translate()))
                    {
                        Find.WindowStack.Add(new Window_RaidDetails(entry));
                    }
                }
            }
            Widgets.EndScrollView();
        }

        private string FormatTicks(int ticks)
        {
            if (ticks <= 0) return "---";
            int day = (ticks / 60000) + 1;
            int hour = (ticks % 60000) / 2500;
            return $"Day {day}, {hour}h";
        }

        private void DrawJumpLabel(Rect rect, string label, GlobalTargetInfo target)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            if (target.IsValid)
            {
                Widgets.DrawHighlightIfMouseover(rect);
                if (Widgets.ButtonInvisible(rect))
                {
                    CameraJumper.TryJumpAndSelect(target);
                    this.Close();

                    var mainTab = Find.WindowStack.WindowOfType<MainTabWindow_Factions>();
                    if (mainTab != null) mainTab.Close();
                }
            }
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }

    public class Window_RaidDetails : Window
    {
        private SpreadLogEntry entry;
        private Vector2 scrollPos;
        public override Vector2 InitialSize => new Vector2(1000f, 780f);

        public Window_RaidDetails(SpreadLogEntry entry)
        {
            this.entry = entry;
            this.doCloseX = true;
            this.draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 35f), "TSA_WD_RaidDet_Title".Translate());
            Text.Font = GameFont.Small;

            Rect outRect = new Rect(0, 45f, inRect.width, inRect.height - 60f);
            float viewHeight = 250f + (entry.attDetails.Count + entry.defDetails.Count) * 28f;
            Rect viewRect = new Rect(0, 0, outRect.width - 20f, viewHeight);

            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            bool isPlayerRaid = entry.message.ToLower().Contains("player colony") || entry.message.ToLower().Contains("player target");

            listing.Label("TSA_WD_RaidDet_HeaderCore".Translate().Colorize(Color.cyan));
            listing.GapLine();

            if (isPlayerRaid)
            {
                listing.Label("TSA_WD_RaidDet_PlayerAssault".Translate().Colorize(Color.yellow));
                listing.Label("TSA_WD_RaidDet_StorytellerPoints".Translate(entry.defStr.ToString("F0")));
                listing.Label("TSA_WD_RaidDet_FactionMult".Translate(entry.ratio.ToString("F2")));
                listing.Label("TSA_WD_RaidDet_FinalPoints".Translate(entry.winChance.ToString("F0")));
            }
            else
            {
                listing.Label("TSA_WD_RaidDet_OffStr".Translate(entry.attStr.ToString("F0")));
                listing.Label("TSA_WD_RaidDet_DefStr".Translate(entry.defStr.ToString("F0")));
                listing.Label("TSA_WD_RaidDet_PowerRatio".Translate(entry.ratio.ToString("F2")));

                if (entry.message.Contains("Aborted"))
                {
                    listing.Label("TSA_WD_RaidDet_Result".Translate("TSA_WD_RaidDet_Aborted".Translate().Colorize(Color.gray)));
                }
                else
                {
                    listing.Label("TSA_WD_RaidDet_SuccessProb".Translate(entry.winChance.ToString("P0")));
                    string resultStr = entry.victory ? "TSA_WD_RaidDet_Victory".Translate().Colorize(Color.green) : "TSA_WD_RaidDet_Defeat".Translate().Colorize(Color.red);
                    listing.Label("TSA_WD_RaidDet_Result".Translate(resultStr));
                }
            }

            listing.Gap(12f);

            if (entry.attDetails.Count > 0)
            {
                listing.Label("TSA_WD_RaidDet_HeaderBreakdown".Translate().Colorize(Color.cyan));
                listing.GapLine();

                string attLossStr = (entry.attLossPct * 100f).ToString("F0");
                listing.Label("TSA_WD_RaidDet_AttackerLoss".Translate(attLossStr).Colorize(Color.yellow));
                foreach (var detail in entry.attDetails) listing.Label($"  - {detail}");

                listing.Gap(12f);

                if (!isPlayerRaid)
                {
                    string defLossStr = (entry.defLossPct * 100f).ToString("F0");
                    listing.Label("TSA_WD_RaidDet_DefenderLoss".Translate(defLossStr).Colorize(Color.yellow));
                    foreach (var detail in entry.defDetails) listing.Label($"  - {detail}");
                }
                else
                {
                    listing.Label("TSA_WD_RaidDet_TargetLabel".Translate(entry.labelB).Colorize(Color.yellow));
                }
            }

            listing.End();
            Widgets.EndScrollView();
        }
    }
}