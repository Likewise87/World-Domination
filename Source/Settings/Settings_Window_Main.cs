using UnityEngine;
using Verse;

namespace TSA_WorldDomination.Settings
{
    public class WorldDominationMod : Mod
    {
        public static WorldDominationSettings settings;

        public WorldDominationMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<WorldDominationSettings>();
        }

        public override string SettingsCategory() => "TSA_WD_Category".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard l = new Listing_Standard();
            l.Begin(inRect);
            var s = WorldDominationMod.settings;

            // NAVIGATION BUTTONS - Hub Layout
            SettingsUI.DrawHeader(l, "TSA_WD_HeaderGeneral".Translate());

            // --- Row 1: Daily Actions, Player Threat, Diplomacy ---
            Rect btnRect1 = l.GetRect(30f);
            float tWidth3 = (btnRect1.width - 20f) / 3f;

            if (Widgets.ButtonText(new Rect(btnRect1.x, btnRect1.y, tWidth3, 30f), "TSA_WD_BtnDailyActions".Translate()))
                Find.WindowStack.Add(new Dialog_DailyActionsSettings());

            if (Widgets.ButtonText(new Rect(btnRect1.x + tWidth3 + 10f, btnRect1.y, tWidth3, 30f), "TSA_WD_BtnRaidMult".Translate()))
                Find.WindowStack.Add(new Dialog_RaidPointMultiplier());

            if (Widgets.ButtonText(new Rect(btnRect1.x + (tWidth3 + 10f) * 2, btnRect1.y, tWidth3, 30f), "TSA_WD_BtnDiplomacy".Translate()))
                Find.WindowStack.Add(new Dialog_DiplomacySettings());

            // --- SURGICAL CHANGE: Action Row (Garrisons & Matrix) ---
            l.Gap(4f);
            Rect subRowRect = l.GetRect(30f);

            // 1. Garrison Settings (Column 2) - ALWAYS VISIBLE
            Rect garrisonBtnRect = new Rect(subRowRect.x + tWidth3 + 10f, subRowRect.y, tWidth3, 30f);
            if (Widgets.ButtonText(garrisonBtnRect, "TSA_WD_BtnGarrisonSettings".Translate()))
            {
                Find.WindowStack.Add(new Dialog_GarrisonSettings());
            }

            // 2. Matrix Button (Column 3) - ONLY IN-GAME (Depends on FactionManager)
            if (Current.ProgramState == ProgramState.Playing)
            {
                Rect matrixBtnRect = new Rect(subRowRect.x + (tWidth3 + 10f) * 2, subRowRect.y, tWidth3, 30f);
                if (Widgets.ButtonText(matrixBtnRect, "TSA_WD_OpenAllegianceMatrix".Translate()))
                {
                    Find.WindowStack.Add(new Dialog_AllegianceLock());
                }
            }

            l.Gap(8f);

            // --- Row 2: World Map Raids, Growth and Expansion ---
            SettingsUI.DrawHeader(l, "TSA_WD_HeaderWorldActions".Translate());

            Rect btnRect2 = l.GetRect(30f);
            float tWidth2 = (btnRect2.width - 10f) / 2f;

            if (Widgets.ButtonText(new Rect(btnRect2.x, btnRect2.y, tWidth2, 30f), "TSA_WD_BtnWorldRaids".Translate()))
                Find.WindowStack.Add(new Dialog_RaidSettings());

            if (Widgets.ButtonText(new Rect(btnRect2.x + tWidth2 + 10f, btnRect2.y, tWidth2, 30f), "TSA_WD_BtnGrowthExpand".Translate()))
                Find.WindowStack.Add(new Dialog_GrowthSettings());

            l.Gap(8f);

            // --- Row 3: Sabotage, Disinformation ---
            SettingsUI.DrawHeader(l, "TSA_WD_HeaderPlayerInteractions".Translate());

            Rect btnRect3 = l.GetRect(30f);
            if (Widgets.ButtonText(new Rect(btnRect3.x, btnRect3.y, tWidth2, 30f), "TSA_WD_BtnSabotage".Translate()))
                Find.WindowStack.Add(new Dialog_SabotageSettings());

            if (Widgets.ButtonText(new Rect(btnRect3.x + tWidth2 + 10f, btnRect3.y, tWidth2, 30f), "TSA_WD_BtnDisinformation".Translate()))
                Find.WindowStack.Add(new Dialog_DisinformationSettings());

            l.Gap(8f);

            // --- Row 4: Misc (Food, World Gen, Notifications) ---
            SettingsUI.DrawHeader(l, "TSA_WD_HeaderMisc".Translate());

            Rect btnRect4 = l.GetRect(30f);
            float miscWidth = (btnRect4.width - 20f) / 3f;

            if (Widgets.ButtonText(new Rect(btnRect4.x, btnRect4.y, miscWidth, 30f), "TSA_WD_BtnOutpostFood".Translate()))
                Find.WindowStack.Add(new Dialog_FoodSettings());

            if (Widgets.ButtonText(new Rect(btnRect4.x + miscWidth + 10f, btnRect4.y, miscWidth, 30f), "TSA_WD_BtnWorldGen".Translate()))
                Find.WindowStack.Add(new Dialog_WorldGenSettings());

            if (Widgets.ButtonText(new Rect(btnRect4.x + (miscWidth + 10f) * 2, btnRect4.y, miscWidth, 30f), "TSA_WD_BtnNotifications".Translate()))
                Find.WindowStack.Add(new Dialog_NotificationSettings());

            // RESET SECTION
            l.Gap(24f);
            SettingsUI.DrawHeader(l, "TSA_WD_HeaderSystem".Translate());

            Rect resetBtnRect = l.GetRect(30f);

            GUI.color = new Color(1f, 0.5f, 0.5f);
            if (Widgets.ButtonText(new Rect(resetBtnRect.x, resetBtnRect.y, resetBtnRect.width, 30f), "TSA_WD_BtnResetAll".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "TSA_WD_ConfirmResetAll".Translate(),
                    () => settings.InitializeDefaults(),
                    destructive: true));
            }
            GUI.color = Color.white;

            l.End();
        }
    }
}