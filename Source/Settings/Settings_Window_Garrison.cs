using UnityEngine;
using Verse;

namespace TSA_WorldDomination.Settings
{
    public class Dialog_GarrisonSettings : Window
    {
        public override Vector2 InitialSize => new Vector2(650f, 600f);

        public Dialog_GarrisonSettings()
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

            Text.Font = GameFont.Medium;
            l.Label("TSA_WD_Garrison_Title".Translate());
            Text.Font = GameFont.Small;
            l.Gap(10f);

            // --- TRIBAL SECTION ---
            SettingsUI.DrawHeader(l, "TSA_WD_HeaderTribalGarrisons".Translate());

            s.kcsgMultTribalT1 = SettingsUI.LabeledSlider(l, "TSA_WD_Garrison_T1".Translate(), s.kcsgMultTribalT1, 0.1f, 10f,
                "TSA_WD_Garrison_Tooltip".Translate(), 0.1f, SliderFormat.Multiplier);

            s.kcsgMultTribalT2 = SettingsUI.LabeledSlider(l, "TSA_WD_Garrison_T2".Translate(), s.kcsgMultTribalT2, 0.1f, 10f,
                "TSA_WD_Garrison_Tooltip".Translate(), 0.1f, SliderFormat.Multiplier);

            s.kcsgMultTribalT3 = SettingsUI.LabeledSlider(l, "TSA_WD_Garrison_T3".Translate(), s.kcsgMultTribalT3, 0.1f, 10f,
                "TSA_WD_Garrison_Tooltip".Translate(), 0.1f, SliderFormat.Multiplier);

            s.kcsgMultTribalT4 = SettingsUI.LabeledSlider(l, "TSA_WD_Garrison_T4".Translate(), s.kcsgMultTribalT4, 0.1f, 10f,
                "TSA_WD_Garrison_Tooltip".Translate(), 0.1f, SliderFormat.Multiplier);


            l.Gap(12f);

            // --- GENERIC SECTION ---
            SettingsUI.DrawHeader(l, "TSA_WD_HeaderGenericGarrisons".Translate());

            s.kcsgMultGenericT1 = SettingsUI.LabeledSlider(l, "TSA_WD_Garrison_T1".Translate(), s.kcsgMultGenericT1, 0.1f, 10f,
                "TSA_WD_Garrison_Tooltip".Translate(), 0.1f, SliderFormat.Multiplier);

            s.kcsgMultGenericT2 = SettingsUI.LabeledSlider(l, "TSA_WD_Garrison_T2".Translate(), s.kcsgMultGenericT2, 0.1f, 10f,
                "TSA_WD_Garrison_Tooltip".Translate(), 0.1f, SliderFormat.Multiplier);

            s.kcsgMultGenericT3 = SettingsUI.LabeledSlider(l, "TSA_WD_Garrison_T3".Translate(), s.kcsgMultGenericT3, 0.1f, 10f,
                "TSA_WD_Garrison_Tooltip".Translate(), 0.1f, SliderFormat.Multiplier);

            s.kcsgMultGenericT4 = SettingsUI.LabeledSlider(l, "TSA_WD_Garrison_T4".Translate(), s.kcsgMultGenericT4, 0.1f, 10f,
                "TSA_WD_Garrison_Tooltip".Translate(), 0.1f, SliderFormat.Multiplier);

            l.Gap(20f);
            if (l.ButtonText("TS_WD_Threat_Reset".Translate()))
            {
                s.ResetGarrisons();
            }

            l.End();
        }
    }
}