using UnityEngine;
using Verse;
using WorldDomination;

namespace TSA_WorldDomination.Settings
{
    public class Dialog_WorldGenSettings : Window
    {
        public override Vector2 InitialSize => new Vector2(850f, 600f);

        public Dialog_WorldGenSettings()
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

            // --- HEADER ---
            SettingsUI.DrawHeader(l, "TSA_WD_Gen_HeaderWeights".Translate());
            l.Gap(10f);

            // Use the WeightSlider style for a clear visual representation of the probability
            float total = s.genWeightT1 + s.genWeightT2 + s.genWeightT3 + s.genWeightT4;

            // Tier 1 (Outposts)
            s.genWeightT1 = SettingsUI.WeightSlider(l,
                "TSA_WD_Tier1".Translate(),
                s.genWeightT1, total, 0f, 200f,
                "TSA_WD_Gen_Tier1Tip".Translate());

            // Tier 2 (Towns)
            s.genWeightT2 = SettingsUI.WeightSlider(l,
                "TSA_WD_Tier2".Translate(),
                s.genWeightT2, total, 0f, 200f,
                "TSA_WD_Gen_Tier2Tip".Translate());

            // Tier 3 (Fortresses)
            s.genWeightT3 = SettingsUI.WeightSlider(l,
                "TSA_WD_Tier3".Translate(),
                s.genWeightT3, total, 0f, 200f,
                "TSA_WD_Gen_Tier3Tip".Translate());

            // Tier 4 (Citadels)
            s.genWeightT4 = SettingsUI.WeightSlider(l,
                "TSA_WD_Tier4".Translate(),
                s.genWeightT4, total, 0f, 200f,
                "TSA_WD_Gen_Tier4Tip".Translate());


            l.Gap(15f);
            if (l.ButtonText("TSA_WD_Gen_Reset".Translate()))
            {
                s.ResetWorldGen(); // Clean, centralized reset
            }

            // --- REROLL SECTION ---
            if (Current.ProgramState == ProgramState.Playing)
            {
                l.Gap(10f);
                GUI.color = Color.red;
                if (l.ButtonText("TSA_WD_Gen_RerollAll".Translate()))
                {
                    // SURGICAL FIX: Wrap execution in a confirmation box
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "TSA_WD_ConfirmReroll".Translate(),
                        () => WorldActions_Utils.RerollAllSettlements(),
                        destructive: true));
                }
                GUI.color = Color.white;
            }

            l.End();
        }
    }
}