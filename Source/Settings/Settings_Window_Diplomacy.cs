using UnityEngine;
using Verse;

namespace TSA_WorldDomination.Settings
{
    public class Dialog_DiplomacySettings : Window
    {
        public override Vector2 InitialSize => new Vector2(1050f, 850f);

        public Dialog_DiplomacySettings()
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

            // --- SECTION 1: DYNAMIC WORLD RELATIONS ---
            SettingsUI.DrawHeader(l, "TSA_WD_Diplo_Header".Translate());

            l.CheckboxLabeled(
                "TSA_WD_Diplo_RandomEnable".Translate(),
                ref s.enableRandomDiplomacy,
                "TSA_WD_Diplo_RandomEnableTooltip".Translate()
            );

            if (s.enableRandomDiplomacy)
            {
                s.diplomacyChangeChance = SettingsUI.LabeledSlider(l, "TSA_WD_Diplo_RandomChance".Translate(), s.diplomacyChangeChance, 0f, 1f,
                    "TSA_WD_Diplo_RandomChanceTooltip".Translate(), 0.01f, SliderFormat.Percent);
            }

            s.revoltChance = SettingsUI.LabeledSlider(l, "TSA_WD_Diplo_RevoltChance".Translate(), s.revoltChance, 0f, 1f,
                "TSA_WD_Diplo_RevoltChanceTooltip".Translate(), 0.002f, SliderFormat.PercentDecimal);

            l.Gap(18f);

            // --- SECTION 2: BUFFS & NERFS ---
            SettingsUI.DrawHeader(l, "TSA_WD_Diplo_GlobalEventsHeader".Translate());

            // 1. Leader Handicap
            DrawEventControls(l, "<b>" + "TSA_WD_Diplo_LeaderHandicap".Translate() + "</b>", "TSA_WD_Diplo_LeaderHandicapTooltip".Translate(),
                ref s.enableLeaderHandicap, ref s.durLeaderHandicapDays, ref s.cdLeaderHandicapDays);

            l.GapLine(12f);

            // 2. Underdog Buff
            DrawEventControls(l, "<b>" + "TSA_WD_Diplo_UnderdogBuff".Translate() + "</b>", "TSA_WD_Diplo_UnderdogBuffTooltip".Translate(),
                ref s.enableUnderdogBuff, ref s.durUnderdogBuffDays, ref s.cdUnderdogBuffDays);

            l.GapLine(12f);

            // 3. Expansionist Zeal (2 Rows: [Duration | Cooldown] then [Chance])
            l.CheckboxLabeled("<b>" + "TSA_WD_Diplo_ExpansionistZeal".Translate() + "</b>", ref s.enableExpansionistZeal, "TSA_WD_Diplo_ExpansionistZealTooltip".Translate());
            if (s.enableExpansionistZeal)
            {
                // Row 1: Duration and Cooldown
                float[] values = new float[] { s.durExpansionistZealDays, s.cdExpansionistZealDays };
                string[] labels = new string[] { "TSA_WD_Slider_Duration".Translate(), "TSA_WD_Slider_Cooldown".Translate() };
                string[] tips = new string[] { "TSA_WD_Slider_DurationTooltip".Translate(), "TSA_WD_Slider_CooldownTooltip".Translate() };

                SettingsUI.MultiColumnSlider(l, labels, values, new Vector2(1f, 30f), tips, 0.5f, SliderFormat.Fixed1);

                s.durExpansionistZealDays = values[0];
                s.cdExpansionistZealDays = values[1];

                // Row 2: Chance (Positioned on the left side to align with Duration)
                s.zealTriggerChance = SettingsUI.LabeledSlider(l, "TSA_WD_Diplo_ZealTriggerChance".Translate(), s.zealTriggerChance, 0f, 1f,
                    "TSA_WD_Diplo_ZealTriggerChanceTooltip".Translate(), 0.01f, SliderFormat.Percent);

                l.Gap(10f);
            }

            l.GapLine(12f);

            // 4. Anti-Leader Coalition
            DrawEventControls(l, "<b>" + "TSA_WD_Diplo_AntiLeader".Translate() + "</b>", "TSA_WD_Diplo_AntiLeaderTooltip".Translate(),
                ref s.enableAntiLeaderCoalition, ref s.durAntiLeaderCoalitionDays, ref s.cdAntiLeaderCoalitionDays);

            // --- SECTION 4: RESET ---
            l.Gap(32f);
            if (l.ButtonText("TSA_WD_Diplo_Reset".Translate()))
            {
                s.ResetDiplomacy();
            }

            l.End();
        }

        private void DrawEventControls(Listing_Standard l, string label, string tooltip, ref bool toggle, ref float duration, ref float cooldown)
        {
            l.CheckboxLabeled(label, ref toggle, tooltip);
            if (toggle)
            {
                float[] values = new float[] { duration, cooldown };
                string[] labels = new string[] { "TSA_WD_Slider_Duration".Translate(), "TSA_WD_Slider_Cooldown".Translate() };
                string[] tips = new string[] { "TSA_WD_Slider_DurationTooltip".Translate(), "TSA_WD_Slider_CooldownTooltip".Translate() };

                SettingsUI.MultiColumnSlider(l, labels, values, new Vector2(1f, 30f), tips, 0.5f, SliderFormat.Fixed1);

                duration = values[0];
                cooldown = values[1];
                l.Gap(6f);
            }
            l.Gap(4f);
        }
    }
}