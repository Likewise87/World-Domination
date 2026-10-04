using UnityEngine;
using Verse;

namespace TSA_WorldDomination.Settings
{
    public class Dialog_DailyActionsSettings : Window
    {
        public override Vector2 InitialSize => new Vector2(850f, 700f);

        public Dialog_DailyActionsSettings()
        {
            doCloseButton = true;
            forcePause = true;
            closeOnClickedOutside = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard l = new Listing_Standard();
            l.Begin(inRect);
            var s = WorldDominationMod.settings;

            // 1. TIER CONTRIBUTIONS (Expanded for T4)
            SettingsUI.DrawHeader(l, "TSA_WD_Daily_HeaderTiers".Translate());

            // Added T4 to the value array
            float[] tierVals = { s.tier1Share, s.tier2Share, s.tier3Share, s.tier4Share };

            string[] tierLabels = {
                "TSA_WD_Tier1".Translate().ToString(),
                "TSA_WD_Tier2".Translate().ToString(),
                "TSA_WD_Tier3".Translate().ToString(),
                "TSA_WD_Tier4".Translate().ToString() // New Label
            };
            string[] tierTips = {
                "TSA_WD_Daily_Tier1Tip".Translate().ToString(),
                "TSA_WD_Daily_Tier2Tip".Translate().ToString(),
                "TSA_WD_Daily_Tier3Tip".Translate().ToString(),
                "TSA_WD_Daily_Tier4Tip".Translate().ToString() // New Tip
            };

            // MultiColumnSlider will now draw 4 columns
            SettingsUI.MultiColumnSlider(l, tierLabels, tierVals, new Vector2(0.1f, 2.5f), tierTips, 0.05f, SliderFormat.Fixed2, 38f);

            s.tier1Share = tierVals[0];
            s.tier2Share = tierVals[1];
            s.tier3Share = tierVals[2];
            s.tier4Share = tierVals[3]; // Save T4 value

            // 2. ACTION LIKELIHOOD WEIGHTS
            l.Gap(12f);
            SettingsUI.DrawHeader(l, "TSA_WD_Daily_HeaderWeights".Translate());

            float total = s.TotalWeight;

            s.weightGrow = SettingsUI.WeightSlider(l, "TSA_WD_Daily_WeightGrow".Translate(), s.weightGrow, total, 0f, 500f, "TSA_WD_Daily_WeightGrowTip".Translate());
            s.weightRaid = SettingsUI.WeightSlider(l, "TSA_WD_Daily_WeightRaid".Translate(), s.weightRaid, total, 0f, 500f, "TSA_WD_Daily_WeightRaidTip".Translate());
            s.weightMinorIncident = SettingsUI.WeightSlider(l, "TSA_WD_Daily_WeightMinor".Translate(), s.weightMinorIncident, total, 0f, 500f, "TSA_WD_Daily_WeightMinorTip".Translate());
            s.weightMajorIncident = SettingsUI.WeightSlider(l, "TSA_WD_Daily_WeightMajor".Translate(), s.weightMajorIncident, total, 0f, 500f, "TSA_WD_Daily_WeightMajorTip".Translate());
            s.weightBuildRoad = SettingsUI.WeightSlider(l, "TSA_WD_Daily_WeightBuildRoad".Translate(), s.weightBuildRoad, total, 0f, 500f, "TSA_WD_Daily_WeightBuildRoadTip".Translate());

            // 3. COOLDOWNS
            l.Gap(12f);
            SettingsUI.DrawHeader(l, "TSA_WD_Daily_HeaderCooldowns".Translate());

            float[] cdRow1 = { s.cooldownGrowDays, s.cooldownExpandDays };
            SettingsUI.MultiColumnSlider(l,
                new[] { "TSA_WD_Daily_CdGrow".Translate().ToString(), "TSA_WD_Daily_CdExpand".Translate().ToString() },
                cdRow1, new Vector2(0f, 10f),
                new[] { "TSA_WD_Daily_CdGrowTip".Translate().ToString(), "TSA_WD_Daily_CdExpandTip".Translate().ToString() },
                0.1f, SliderFormat.Fixed1, 38f);

            s.cooldownGrowDays = cdRow1[0];
            s.cooldownExpandDays = cdRow1[1];

            float[] cdRow2 = { s.cooldownRaidDays, s.cooldownBeingRaidedDays };
            SettingsUI.MultiColumnSlider(l,
                new[] { "TSA_WD_Daily_CdRaid".Translate().ToString(), "TSA_WD_Daily_CdBeingRaided".Translate().ToString() },
                cdRow2, new Vector2(0f, 10f),
                new[] { "TSA_WD_Daily_CdRaidTip".Translate().ToString(), "TSA_WD_Daily_CdBeingRaidedTip".Translate().ToString() },
                0.1f, SliderFormat.Fixed1, 38f);

            s.cooldownRaidDays = cdRow2[0];
            s.cooldownBeingRaidedDays = cdRow2[1];

            l.Gap(20f);
            if (l.ButtonText("TSA_WD_Daily_Reset".Translate()))
                s.ResetDailyActions();

            l.End();
        }
    }
}