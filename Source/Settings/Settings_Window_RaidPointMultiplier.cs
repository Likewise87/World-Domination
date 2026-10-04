using UnityEngine;
using Verse;

namespace TSA_WorldDomination.Settings
{
    public class Dialog_RaidPointMultiplier : Window
    {
        public override Vector2 InitialSize => new Vector2(850f, 850f); // Slightly increased height for the extra slider
        public Dialog_RaidPointMultiplier() { doCloseButton = true; forcePause = true; closeOnClickedOutside = true; }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard l = new Listing_Standard();
            l.Begin(inRect);
            var s = WorldDominationMod.settings;

            SettingsUI.DrawHeader(l, "TSA_WD_Threat_HeaderRadius".Translate());

            // 1. Radius Settings
            s.playerInfluenceBase = SettingsUI.LabeledSlider(l, "TS_WD_Threat_InfBase".Translate(), s.playerInfluenceBase, 0f, 40f,
                "TS_WD_Threat_InfBaseTooltip".Translate(), 1.0f, SliderFormat.Fixed0);

            s.playerInfluenceWealthRate = SettingsUI.LabeledSlider(l, "TS_WD_Threat_InfWealth".Translate(), s.playerInfluenceWealthRate, 0.1f, 5f,
                "TS_WD_Threat_InfWealthTooltip".Translate(), 0.05f, SliderFormat.Fixed2);

            s.playerInfluenceTimeRate = SettingsUI.LabeledSlider(l, "TS_WD_Threat_InfTime".Translate(), s.playerInfluenceTimeRate, 0f, 3f,
                "TS_WD_Threat_InfTimeTooltip".Translate(), 0.05f, SliderFormat.Fixed2);

            SettingsUI.DrawHeader(l, "TSA_WD_Threat_HeaderCalc".Translate());

            // 2. Raid Point Floors
            s.minRaidPoints = SettingsUI.LabeledSlider(l, "TS_WD_Threat_MinPoints".Translate(), s.minRaidPoints, 50f, 500f,
                "TS_WD_Threat_MinPointsTooltip".Translate(), 1.0f, SliderFormat.Fixed0);

            s.maxRaidPoints = SettingsUI.LabeledSlider(l, "TS_WD_Threat_MaxPoints".Translate(), s.maxRaidPoints, 1000f, 20000f,
                "TS_WD_Threat_MaxPointsTooltip".Translate(), 100f, SliderFormat.Fixed0);

            l.Gap(6f);

            s.minRaidPointsMultiplier = SettingsUI.LabeledSlider(l, "TS_WD_Threat_MinMult".Translate(), s.minRaidPointsMultiplier, 0.1f, 2f,
                "TS_WD_Threat_MinMultTooltip".Translate(), 0.05f, SliderFormat.Multiplier);

            s.maxThreatMultiplier = SettingsUI.LabeledSlider(l, "TS_WD_Threat_MaxMult".Translate(), s.maxThreatMultiplier, 1f, 20f,
                "TS_WD_Threat_MaxMultTooltip".Translate(), 0.1f, SliderFormat.Multiplier);

            l.GapLine();
            l.Label("TS_WD_Threat_DistanceMods".Translate() + ":");

            // 3. Distance Multipliers
            s.threatDistMult50 = SettingsUI.LabeledSlider(l, "TS_WD_Threat_Dist50".Translate(), s.threatDistMult50, 0f, 2f,
                "TS_WD_Threat_Dist50Tooltip".Translate(), 0.01f, SliderFormat.Percent);

            s.threatDistMult75 = SettingsUI.LabeledSlider(l, "TS_WD_Threat_Dist75".Translate(), s.threatDistMult75, 0f, 2f,
                "TS_WD_Threat_Dist75Tooltip".Translate(), 0.01f, SliderFormat.Percent);

            s.threatDistMult100 = SettingsUI.LabeledSlider(l, "TS_WD_Threat_Dist100".Translate(), s.threatDistMult100, 0f, 2f,
                "TS_WD_Threat_Dist100Tooltip".Translate(), 0.01f, SliderFormat.Percent);

            l.GapLine();
            l.Label("TS_WD_Threat_TierMods".Translate() + ":");

            // 4. Tier Multipliers (Now including T4)
            s.threatTier1Mult = SettingsUI.LabeledSlider(l, "TS_WD_Threat_Tier1".Translate(), s.threatTier1Mult, 0f, 4f,
                "TS_WD_Threat_Tier1Tooltip".Translate(), 0.01f, SliderFormat.Percent);

            s.threatTier2Mult = SettingsUI.LabeledSlider(l, "TS_WD_Threat_Tier2".Translate(), s.threatTier2Mult, 0f, 4f,
                "TS_WD_Threat_Tier2Tooltip".Translate(), 0.01f, SliderFormat.Percent);

            s.threatTier3Mult = SettingsUI.LabeledSlider(l, "TS_WD_Threat_Tier3".Translate(), s.threatTier3Mult, 0f, 4f,
                "TS_WD_Threat_Tier3Tooltip".Translate(), 0.01f, SliderFormat.Percent);

            // Added T4 Slider
            s.threatTier4Mult = SettingsUI.LabeledSlider(l, "TS_WD_Threat_Tier4".Translate(), s.threatTier4Mult, 0f, 6f,
                "TS_WD_Threat_Tier4Tooltip".Translate(), 0.01f, SliderFormat.Percent);

            l.Gap(20f);
            if (l.ButtonText("TS_WD_Threat_Reset".Translate())) s.ResetThreat();
            l.End();
        }
    }
}