using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace TSA_WorldDomination.Settings
{
    public class Dialog_FoodSettings : Window
    {
        public override Vector2 InitialSize => new Vector2(800f, 600f);

        public Dialog_FoodSettings()
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

            // --- GLOBAL TOGGLE ---
            SettingsUI.DrawHeader(l, "TSA_WD_Food_HeaderGlobal".Translate(), Color.cyan);
            l.CheckboxLabeled("TSA_WD_Food_Active".Translate(), ref s.foodLogisticsActive, "TSA_WD_Food_ActiveTooltip".Translate());

            if (s.foodLogisticsActive)
            {
                // --- CONSUMPTION & PRODUCTION ---
                SettingsUI.DrawHeader(l, "TSA_WD_Food_HeaderValues".Translate());

                s.foodConsumptionPerPawn = SettingsUI.LabeledSlider(l,
                    "TSA_WD_Food_ConsPawn".Translate(), s.foodConsumptionPerPawn, 0.1f, 5.0f,
                    "TSA_WD_Food_ConsPawnTooltip".Translate(), 0.1f, SliderFormat.Fixed1);

                s.foodProductionPerSkill = SettingsUI.LabeledSlider(l,
                    "TSA_WD_Food_ProdSkill".Translate(), s.foodProductionPerSkill, 0.1f, 3.0f,
                    "TSA_WD_Food_ProdSkillTooltip".Translate(), 0.1f, SliderFormat.Fixed1);

                // --- DISTANCE & RANGE ---
                SettingsUI.DrawHeader(l, "TSA_WD_Food_HeaderRange".Translate());

                s.maxLogisticsRange = (int)SettingsUI.LabeledSlider(l,
                    "TSA_WD_Food_MaxRange".Translate(), s.maxLogisticsRange, 5f, 50f,
                    "TSA_WD_Food_MaxRangeTooltip".Translate(), 1f, SliderFormat.Fixed0);

                // --- EFFICIENCY FALLOFF ---
                l.Gap(10f);
                l.Label("TSA_WD_Food_EffTitle".Translate() + ":");

                // Using the MultiColumnSlider from your library for the 3 percentage steps
                string[] labels = { "60%", "80%", "100%" };
                float[] values = { s.effAt60, s.effAt80, s.effAt100 };
                string[] tooltips = { "TSA_WD_Food_Eff60Tip".Translate(), "TSA_WD_Food_Eff80Tip".Translate(), "TSA_WD_Food_Eff100Tip".Translate() };

                SettingsUI.MultiColumnSlider(l, labels, values, new Vector2(0f, 1f), tooltips, 0.01f, SliderFormat.Percent);

                // Update settings back from the multi-column array
                s.effAt60 = values[0];
                s.effAt80 = values[1];
                s.effAt100 = values[2];

                l.Gap(20f);
                if (l.ButtonText("TSA_WD_Food_Reset".Translate())) s.ResetFoodLogistics();
            }
            else
            {
                l.Gap(10f);
                GUI.color = Color.gray;
                l.Label("  <i>" + "TSA_WD_Food_DisabledWarning".Translate() + "</i>");
                GUI.color = Color.white;
            }

            l.End();
        }
    }
}