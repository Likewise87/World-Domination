using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace TSA_WorldDomination.Settings
{
    public class Dialog_RaidSettings : Window
    {
        public override Vector2 InitialSize => new Vector2(1050f, 820f);
        public Dialog_RaidSettings() { doCloseButton = true; forcePause = true; closeOnClickedOutside = true; }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard l = new Listing_Standard();
            l.Begin(inRect);
            var s = WorldDominationMod.settings;

            // --- SECTION 1: WORLD RAIDS ON PLAYER ---
            SettingsUI.DrawHeader(l, "TSA_WD_Raid_HeaderPlayer".Translate());

            l.CheckboxLabeled("TSA_WD_Raid_AllowPlayer".Translate(), ref s.allowPlayerRaid, "TSA_WD_Raid_AllowPlayerTooltip".Translate());

            if (s.allowPlayerRaid)
            {
                s.cooldownPlayerRaidDays = SettingsUI.LabeledSlider(l, "TSA_WD_Raid_CdPlayer".Translate(), s.cooldownPlayerRaidDays, 0f, 15f,
                    "TSA_WD_Raid_CdPlayerTooltip".Translate(), 0.5f, SliderFormat.Fixed1);

                s.raidCostPct = SettingsUI.LabeledSlider(l, "TSA_WD_Raid_CostPct".Translate(), s.raidCostPct, 0f, 0.5f,
                    "TSA_WD_Raid_CostPctTooltip".Translate(), 0.01f, SliderFormat.Percent);
            }
            else
            {
                l.Gap(6f);
                GUI.color = Color.gray;
                l.Label("    <i>" + "TSA_WD_Raid_DisabledPlayer".Translate() + "</i>");
                GUI.color = Color.white;
                l.Gap(6f);
            }

            l.CheckboxLabeled("TSA_WD_Raid_AllowOutpost".Translate(), ref s.allowPlayerOutpostRaid, "TSA_WD_Raid_AllowOutpostTooltip".Translate());

            // --- SECTION 2: SIMULATION PARAMETERS ---
            SettingsUI.DrawHeader(l, "TSA_WD_Raid_HeaderSim".Translate());

            s.raidTargetRadius = SettingsUI.LabeledSlider(l, "TSA_WD_Raid_TargetRad".Translate(), s.raidTargetRadius, 5f, 200f,
                "TSA_WD_Raid_TargetRadTooltip".Translate(), 1f, SliderFormat.Fixed0);

            s.raidAttackerAllyRadius = SettingsUI.LabeledSlider(l, "TSA_WD_Raid_AttReinfRad".Translate(), s.raidAttackerAllyRadius, 5f, 200f,
                "TSA_WD_Raid_AttReinfRadTooltip".Translate(), 1f, SliderFormat.Fixed0);

            s.raidDefenderAllyRadius = SettingsUI.LabeledSlider(l, "TSA_WD_Raid_DefReinfRad".Translate(), s.raidDefenderAllyRadius, 5f, 200f,
                "TSA_WD_Raid_DefReinfRadTooltip".Translate(), 1f, SliderFormat.Fixed0);

            l.Gap(6f);

            s.minRaidRatio = SettingsUI.LabeledSlider(l, "TSA_WD_Raid_MinRatio".Translate(), s.minRaidRatio, 0.5f, 2.0f,
                "TSA_WD_Raid_MinRatioTooltip".Translate(), 0.05f, SliderFormat.Multiplier);

            s.razeChance = SettingsUI.LabeledSlider(l, "TSA_WD_Raid_RazeChance".Translate(), s.razeChance, 0f, 1f,
                "TSA_WD_Raid_RazeChanceTooltip".Translate(), 0.05f, SliderFormat.Percent);

            // --- SECTION 3: OUTCOME TIERS ---
            SettingsUI.DrawHeader(l, "TSA_WD_Raid_HeaderOutcome".Translate());
            l.Gap(2f);

            DrawTableHeaders(l.GetRect(24f));
            foreach (var outcome in s.raidOutcomes)
            {
                DrawOutcomeRow(l.GetRect(32f), outcome);
                l.Gap(2f);
            }

            l.Gap(20f);
            if (l.ButtonText("TSA_WD_Raid_Reset".Translate())) s.ResetRaids();

            l.End();
        }

        private void DrawTableHeaders(Rect r)
        {
            float w = r.width / 6f;
            Text.Font = GameFont.Small;

            for (int i = 0; i < 6; i++)
            {
                Rect headerRect = new Rect(r.x + w * i, r.y, w - 10, r.height);
                Widgets.Label(headerRect, ("TSA_WD_Raid_Col" + i).Translate());
                TooltipHandler.TipRegion(headerRect, ("TSA_WD_Raid_Col" + i + "Tip").Translate());
            }
        }

        private void DrawOutcomeRow(Rect r, RaidOutcome o)
        {
            float w = r.width / 6f;

            // SURGICAL FIX: Replace HorizontalSlider with a Label to lock the threshold
            Rect threshRect = new Rect(r.x, r.y, w - 10, r.height);
            // Add a subtle background box so it still looks like a table cell
            Widgets.DrawBoxSolidWithOutline(threshRect, new Color(1f, 1f, 1f, 0.05f), new Color(1f, 1f, 1f, 0.1f));
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(threshRect, (o.threshold.ToString("F2") + "x").Colorize(Color.yellow));
            Text.Anchor = TextAnchor.UpperLeft;

            // Keep the rest as sliders so the player can still tune the balance/likelihoods
            string winStr = ((o.winChance * 100).ToString("F0") + "%").Colorize(Color.cyan);
            o.winChance = Widgets.HorizontalSlider(new Rect(r.x + w, r.y, w - 10, r.height), o.winChance, 0f, 1.0f, false, winStr);

            string attWinStr = ("-" + (o.attLossWin * 100).ToString("F0") + "%").Colorize(Color.cyan);
            o.attLossWin = Widgets.HorizontalSlider(new Rect(r.x + w * 2, r.y, w - 10, r.height), o.attLossWin, 0f, 1.0f, false, attWinStr);

            string defLossStr = ("-" + (o.defLossLoss * 100).ToString("F0") + "%").Colorize(Color.cyan);
            o.defLossLoss = Widgets.HorizontalSlider(new Rect(r.x + w * 3, r.y, w - 10, r.height), o.defLossLoss, 0f, 1.0f, false, defLossStr);

            string attLossStr = ("-" + (o.attLossLoss * 100).ToString("F0") + "%").Colorize(Color.cyan);
            o.attLossLoss = Widgets.HorizontalSlider(new Rect(r.x + w * 4, r.y, w - 10, r.height), o.attLossLoss, 0f, 1.0f, false, attLossStr);

            string defWinStr = ("-" + (o.defLossWin * 100).ToString("F0") + "%").Colorize(Color.cyan);
            o.defLossWin = Widgets.HorizontalSlider(new Rect(r.x + w * 5, r.y, w - 10, r.height), o.defLossWin, 0f, 1.0f, false, defWinStr);
        }
    }
}