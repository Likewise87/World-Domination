using UnityEngine;
using Verse;

namespace TSA_WorldDomination.Settings
{
    public class Dialog_GrowthSettings : Window
    {
        public override Vector2 InitialSize => new Vector2(850f, 880f); // Increased height for T4 row
        public Dialog_GrowthSettings() { doCloseButton = true; forcePause = true; closeOnClickedOutside = true; }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard l = new Listing_Standard();
            l.Begin(inRect);
            var s = WorldDominationMod.settings;

            // ================= SECTION 1: GROWTH =================
            SettingsUI.DrawHeader(l, "TSA_WD_Growth_HeaderGrowth".Translate());

            s.maxSettlements = (int)SettingsUI.LabeledSlider(l, "TSA_WD_Growth_MaxSettlements".Translate(), (float)s.maxSettlements, 30f, 800f,
                "TSA_WD_Growth_MaxSettlementsTooltip".Translate(), 1.0f, SliderFormat.Fixed0);

            s.baseGrowthAmount = SettingsUI.LabeledSlider(l, "TSA_WD_Growth_BaseGrowth".Translate(), s.baseGrowthAmount, 10f, 300f,
                "TSA_WD_Growth_BaseGrowthTooltip".Translate(), 5.0f, SliderFormat.Fixed0);

            s.growthScalingIntensity = SettingsUI.LabeledSlider(l, "TSA_WD_Growth_Scaling".Translate(), s.growthScalingIntensity, 0f, 1f,
                "TSA_WD_Growth_ScalingTooltip".Translate(), 0.05f, SliderFormat.Percent);

            l.Gap(12f);

            // ================= SECTION 2: INCIDENTS =================
            SettingsUI.DrawHeader(l, "TSA_WD_Growth_HeaderIncidents".Translate());

            s.minorIncidentSeverity = SettingsUI.LabeledSlider(l, "TSA_WD_Growth_MinorInc".Translate(), s.minorIncidentSeverity, 10f, 500f,
                "TSA_WD_Growth_MinorIncTooltip".Translate(), 5.0f, SliderFormat.Fixed0);

            s.majorIncidentSeverity = SettingsUI.LabeledSlider(l, "TSA_WD_Growth_MajorInc".Translate(), s.majorIncidentSeverity, 100f, 1500f,
                "TSA_WD_Growth_MajorIncTooltip".Translate(), 10.0f, SliderFormat.Fixed0);

            l.Gap(12f);

            // ================= SECTION 3: EXPANSION & DENSITY =================
            SettingsUI.DrawHeader(l, "TSA_WD_Growth_HeaderExpansion".Translate());

            s.expandMinRadius = (int)SettingsUI.LabeledSlider(l, "TSA_WD_Growth_MinRadius".Translate(), (float)s.expandMinRadius, 2f, 20f,
                "TSA_WD_Growth_MinRadiusTooltip".Translate(), 1.0f, SliderFormat.Fixed0);

            s.expandMaxRadius = (int)SettingsUI.LabeledSlider(l, "TSA_WD_Growth_MaxRadius".Translate(), (float)s.expandMaxRadius, 5f, 80f,
                "TSA_WD_Growth_MaxRadiusTooltip".Translate(), 1.0f, SliderFormat.Fixed0);

            if (s.expandMaxRadius < s.expandMinRadius) s.expandMaxRadius = s.expandMinRadius;

            l.Gap(6f);

            s.expansionSuccessChance = SettingsUI.LabeledSlider(l, "TSA_WD_Growth_ExpandChance".Translate(), s.expansionSuccessChance, 0.05f, 1f,
                "TSA_WD_Growth_ExpandChanceTooltip".Translate(), 0.05f, SliderFormat.Percent);

            l.Gap(6f);

            s.localMaxT1 = (int)SettingsUI.LabeledSlider(l, "TSA_WD_Growth_LocalMaxT1".Translate(), (float)s.localMaxT1, 1f, 20f,
                "TSA_WD_Growth_LocalMaxT1Tooltip".Translate(), 1.0f, SliderFormat.Fixed0);

            s.localMaxT2 = (int)SettingsUI.LabeledSlider(l, "TSA_WD_Growth_LocalMaxT2".Translate(), (float)s.localMaxT2, 1f, 15f,
                "TSA_WD_Growth_LocalMaxT2Tooltip".Translate(), 1.0f, SliderFormat.Fixed0);

            s.localMaxT3 = (int)SettingsUI.LabeledSlider(l, "TSA_WD_Growth_LocalMaxT3".Translate(), (float)s.localMaxT3, 1f, 10f,
                "TSA_WD_Growth_LocalMaxT3Tooltip".Translate(), 1.0f, SliderFormat.Fixed0);

            // New T4 Local Cap
            s.localMaxT4 = (int)SettingsUI.LabeledSlider(l, "TSA_WD_Growth_LocalMaxT4".Translate(), (float)s.localMaxT4, 1f, 5f,
                "TSA_WD_Growth_LocalMaxT4Tooltip".Translate(), 1.0f, SliderFormat.Fixed0);

            l.Gap(12f);

            // ================= SECTION 4: Roads =================
            SettingsUI.DrawHeader(l, "TSA_WD_Growth_Roads".Translate());
            s.maxRoadRange = (int)SettingsUI.LabeledSlider(l, "TSA_WD_Daily_MaxRoadRange".Translate(), (float)s.maxRoadRange, 5f, 150f, "TSA_WD_Daily_MaxRoadRangeTip".Translate(), 1f, SliderFormat.Fixed0);

            // ================= SECTION 5: RESET =================
            l.Gap(24f);
            if (l.ButtonText("TSA_WD_Growth_Reset".Translate())) s.ResetGrowth();
            l.End();
        }
    }
}